using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace YNWpsTranslatorHelper
{
    internal sealed class LocalHttpServer : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly TranslationService _service;
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer();
        private volatile bool _running;

        public LocalHttpServer(IPAddress address, int port, TranslationService service)
        {
            _listener = new TcpListener(address, port);
            _service = service;
            _json.MaxJsonLength = 16 * 1024 * 1024;
        }

        public void Run()
        {
            _listener.Start();
            _running = true;
            while (_running)
            {
                try
                {
                    var client = _listener.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(HandleClient, client);
                }
                catch (SocketException)
                {
                    if (_running)
                    {
                        throw;
                    }
                }
            }
        }

        private void HandleClient(object state)
        {
            using (var client = (TcpClient)state)
            using (var stream = client.GetStream())
            {
                try
                {
                    client.ReceiveTimeout = 120000;
                    client.SendTimeout = 120000;
                    var request = ReadRequest(stream);
                    if (request == null)
                    {
                        return;
                    }

                    if (request.Method == "OPTIONS")
                    {
                        WriteResponse(stream, 204, "");
                        return;
                    }

                    object response = Route(request);
                    WriteResponse(stream, 200, _json.Serialize(response));
                }
                catch (UserVisibleException ex)
                {
                    WriteResponse(stream, 400, _json.Serialize(new { ok = false, error = ex.Message }));
                }
                catch (Exception ex)
                {
                    Log.Write("Request error: " + ex);
                    WriteResponse(stream, 500, _json.Serialize(new { ok = false, error = "本机翻译助手发生错误，请查看日志。" }));
                }
            }
        }

        private object Route(HttpRequest request)
        {
            if (request.Method == "GET" && request.Path == "/health")
            {
                return new { ok = true, version = "1.0.0" };
            }
            if (request.Method == "GET" && request.Path == "/settings")
            {
                return _service.GetSettings();
            }
            if (request.Method == "POST" && request.Path == "/settings")
            {
                return _service.SaveSettings(ParseBody(request.Body));
            }
            if (request.Method == "POST" && request.Path == "/test")
            {
                return _service.Test();
            }
            if (request.Method == "GET" && request.Path == "/glossary")
            {
                return _service.GetGlossary();
            }
            if (request.Method == "POST" && request.Path == "/glossary")
            {
                return _service.SaveGlossary(ParseBody(request.Body));
            }
            if (request.Method == "POST" && request.Path == "/translate")
            {
                return _service.Translate(ParseBody(request.Body));
            }
            throw new UserVisibleException("未知的本机翻译助手请求。");
        }

        private Dictionary<string, object> ParseBody(string body)
        {
            if (String.IsNullOrWhiteSpace(body))
            {
                return new Dictionary<string, object>();
            }
            var result = _json.DeserializeObject(body) as Dictionary<string, object>;
            if (result == null)
            {
                throw new UserVisibleException("请求数据格式不正确。");
            }
            return result;
        }

        private static HttpRequest ReadRequest(NetworkStream stream)
        {
            var headerBytes = new List<byte>();
            int matched = 0;
            while (headerBytes.Count < 65536)
            {
                int value = stream.ReadByte();
                if (value < 0)
                {
                    return null;
                }
                byte b = (byte)value;
                headerBytes.Add(b);
                if ((matched == 0 && b == 13) ||
                    (matched == 1 && b == 10) ||
                    (matched == 2 && b == 13) ||
                    (matched == 3 && b == 10))
                {
                    matched++;
                    if (matched == 4)
                    {
                        break;
                    }
                }
                else
                {
                    matched = b == 13 ? 1 : 0;
                }
            }

            var headerText = Encoding.ASCII.GetString(headerBytes.ToArray());
            var lines = headerText.Split(new[] { "\r\n" }, StringSplitOptions.None);
            if (lines.Length == 0)
            {
                return null;
            }
            var firstLine = lines[0].Split(' ');
            if (firstLine.Length < 2)
            {
                return null;
            }

            int contentLength = 0;
            for (int i = 1; i < lines.Length; i++)
            {
                int colon = lines[i].IndexOf(':');
                if (colon <= 0)
                {
                    continue;
                }
                var name = lines[i].Substring(0, colon).Trim();
                var value = lines[i].Substring(colon + 1).Trim();
                if (name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
                {
                    Int32.TryParse(value, out contentLength);
                }
            }

            var bodyBytes = new byte[contentLength];
            int offset = 0;
            while (offset < contentLength)
            {
                int read = stream.Read(bodyBytes, offset, contentLength - offset);
                if (read <= 0)
                {
                    break;
                }
                offset += read;
            }

            return new HttpRequest
            {
                Method = firstLine[0].ToUpperInvariant(),
                Path = firstLine[1].Split('?')[0],
                Body = Encoding.UTF8.GetString(bodyBytes, 0, offset)
            };
        }

        private static void WriteResponse(NetworkStream stream, int statusCode, string body)
        {
            string statusText = statusCode == 200 ? "OK" :
                statusCode == 204 ? "No Content" :
                statusCode == 400 ? "Bad Request" : "Internal Server Error";
            var bodyBytes = Encoding.UTF8.GetBytes(body ?? "");
            var headers =
                "HTTP/1.1 " + statusCode + " " + statusText + "\r\n" +
                "Content-Type: application/json; charset=utf-8\r\n" +
                "Access-Control-Allow-Origin: *\r\n" +
                "Access-Control-Allow-Headers: Content-Type\r\n" +
                "Access-Control-Allow-Methods: GET, POST, OPTIONS\r\n" +
                "Cache-Control: no-store\r\n" +
                "Connection: close\r\n" +
                "Content-Length: " + bodyBytes.Length + "\r\n\r\n";
            var headerBytes = Encoding.ASCII.GetBytes(headers);
            stream.Write(headerBytes, 0, headerBytes.Length);
            if (bodyBytes.Length > 0)
            {
                stream.Write(bodyBytes, 0, bodyBytes.Length);
            }
            stream.Flush();
        }

        public void Dispose()
        {
            _running = false;
            try
            {
                _listener.Stop();
            }
            catch
            {
            }
        }

        private sealed class HttpRequest
        {
            public string Method;
            public string Path;
            public string Body;
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;

namespace YNWpsTranslatorHelper
{
    // 通过兼容 OpenAI 的 /chat/completions 接口调用大模型完成翻译。
    // 默认对接智谱 GLM-4-Flash，只要在设置里改 BaseUrl / Model / ApiKey
    // 即可切换到 Gemini、硅基流动、Kimi、DeepSeek 等同样兼容 OpenAI 协议的服务。
    internal sealed class LlmTranslator
    {
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer();

        static LlmTranslator()
        {
            // 老版 .NET Framework 默认可能不启用 TLS1.2，HTTPS 接口会握手失败。
            // 用数值常量避免在旧框架上因枚举缺失而编译报错。3072=Tls12，768=Tls11。
            try
            {
                ServicePointManager.SecurityProtocol =
                    (SecurityProtocolType)3072 | (SecurityProtocolType)768 | SecurityProtocolType.Tls;
            }
            catch
            {
            }
        }

        public string Translate(string source, IList<KeyValuePair<string, string>> glossary, AppSettings settings)
        {
            if (String.IsNullOrWhiteSpace(source))
            {
                return "";
            }

            string url = String.IsNullOrWhiteSpace(settings.BaseUrl) ? AppSettings.DefaultBaseUrl : settings.BaseUrl.Trim();
            string model = String.IsNullOrWhiteSpace(settings.Model) ? AppSettings.DefaultModel : settings.Model.Trim();
            string targetLang = String.IsNullOrWhiteSpace(settings.TargetLang) ? AppSettings.DefaultTargetLang : settings.TargetLang.Trim();

            var messages = new List<object>
            {
                new Dictionary<string, object>
                {
                    { "role", "system" },
                    { "content", BuildSystemPrompt(targetLang, glossary) }
                },
                new Dictionary<string, object>
                {
                    { "role", "user" },
                    { "content", source }
                }
            };

            var payload = new Dictionary<string, object>
            {
                { "model", model },
                { "messages", messages },
                { "temperature", 0.2 },
                { "max_tokens", 4096 },
                { "stream", false }
            };

            byte[] requestBytes = Encoding.UTF8.GetBytes(_json.Serialize(payload));

            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST";
            request.ContentType = "application/json; charset=utf-8";
            request.Headers["Authorization"] = "Bearer " + (settings.ApiKey ?? "").Trim();
            request.Accept = "application/json";
            request.Timeout = 60000;
            request.ReadWriteTimeout = 60000;
            request.ContentLength = requestBytes.Length;

            try
            {
                using (var stream = request.GetRequestStream())
                {
                    stream.Write(requestBytes, 0, requestBytes.Length);
                }

                using (var response = (HttpWebResponse)request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                {
                    return ParseSuccess(reader.ReadToEnd());
                }
            }
            catch (WebException ex)
            {
                throw new UserVisibleException(DescribeWebException(ex));
            }
        }

        private static string BuildSystemPrompt(string targetLang, IList<KeyValuePair<string, string>> glossary)
        {
            var sb = new StringBuilder();
            sb.Append("你是一个专业的翻译引擎。请自动识别输入文本的语言（可能是中文、英文或印尼语），");
            sb.Append("并把它准确翻译成").Append(targetLang).Append("。");
            sb.Append("严格遵守以下要求：\n");
            sb.Append("1. 只输出").Append(targetLang).Append("译文本身，不要输出任何解释、注释、拼音、引号或原文。\n");
            sb.Append("2. 保留原文中的数字、单位、英文缩写、型号和专有名词。\n");
            sb.Append("3. 保留原文的换行结构，逐行对应翻译。\n");
            sb.Append("4. 如果原文本身已经是").Append(targetLang).Append("，或为纯数字/符号，则原样返回。");

            if (glossary != null && glossary.Count > 0)
            {
                sb.Append("\n5. 如果原文中出现以下术语，必须使用右侧指定的译法（仅当目标语种与右侧译法语言一致时适用）：\n");
                foreach (var entry in glossary)
                {
                    if (String.IsNullOrWhiteSpace(entry.Key))
                    {
                        continue;
                    }
                    sb.Append("   ").Append(entry.Key).Append(" => ").Append(entry.Value).Append("\n");
                }
            }
            return sb.ToString();
        }

        private string ParseSuccess(string responseText)
        {
            var root = _json.DeserializeObject(responseText) as Dictionary<string, object>;
            if (root == null)
            {
                throw new UserVisibleException("大模型返回了无法识别的数据。");
            }

            ThrowIfApiError(root);

            object choicesObj;
            if (!root.TryGetValue("choices", out choicesObj))
            {
                throw new UserVisibleException("大模型未返回翻译结果（无 choices 字段）。");
            }

            var choices = AsArray(choicesObj);
            if (choices == null || choices.Length == 0)
            {
                throw new UserVisibleException("大模型未返回翻译结果。");
            }

            var firstChoice = choices[0] as Dictionary<string, object>;
            if (firstChoice == null)
            {
                throw new UserVisibleException("大模型返回的结果格式异常。");
            }

            var message = GetDict(firstChoice, "message");
            if (message == null)
            {
                throw new UserVisibleException("大模型返回的结果缺少 message。");
            }

            object contentObj;
            if (!message.TryGetValue("content", out contentObj) || contentObj == null)
            {
                throw new UserVisibleException("大模型返回了空译文。");
            }

            string content = Convert.ToString(contentObj).Trim();
            if (content.Length == 0)
            {
                throw new UserVisibleException("大模型返回了空译文。");
            }
            return content;
        }

        private void ThrowIfApiError(Dictionary<string, object> root)
        {
            object errorObj;
            if (!root.TryGetValue("error", out errorObj) || errorObj == null)
            {
                return;
            }
            var error = errorObj as Dictionary<string, object>;
            string code = error != null && error.ContainsKey("code") ? Convert.ToString(error["code"]) : "";
            string message = error != null && error.ContainsKey("message")
                ? Convert.ToString(error["message"])
                : Convert.ToString(errorObj);
            throw new UserVisibleException("翻译接口错误 " + code + "：" + FriendlyError(code, message));
        }

        private string DescribeWebException(WebException ex)
        {
            var httpResponse = ex.Response as HttpWebResponse;
            if (httpResponse != null)
            {
                string body = "";
                try
                {
                    using (var reader = new StreamReader(httpResponse.GetResponseStream(), Encoding.UTF8))
                    {
                        body = reader.ReadToEnd();
                    }
                }
                catch
                {
                }

                string code = "";
                string message = body;
                try
                {
                    var root = _json.DeserializeObject(body) as Dictionary<string, object>;
                    if (root != null)
                    {
                        object errorObj;
                        if (root.TryGetValue("error", out errorObj))
                        {
                            var error = errorObj as Dictionary<string, object>;
                            if (error != null)
                            {
                                code = error.ContainsKey("code") ? Convert.ToString(error["code"]) : "";
                                message = error.ContainsKey("message") ? Convert.ToString(error["message"]) : body;
                            }
                            else
                            {
                                message = Convert.ToString(errorObj);
                            }
                        }
                    }
                }
                catch
                {
                }

                int status = (int)httpResponse.StatusCode;
                if (String.IsNullOrEmpty(code))
                {
                    code = status.ToString();
                }
                return "翻译接口返回 HTTP " + status + "：" + FriendlyError(code, message);
            }
            return "无法连接翻译接口：" + ex.Message + "（请检查网络、BaseUrl 是否正确）。";
        }

        private static string FriendlyError(string code, string fallback)
        {
            switch (code)
            {
                case "1000":
                case "401":
                    return "鉴权失败，请检查 API Key 是否填写正确。";
                case "1002":
                    return "API Key 无效或已被禁用，请到智谱控制台重新生成。";
                case "1003":
                    return "API Key 缺失或格式不正确。";
                case "1113":
                case "1112":
                    return "账户余额或免费额度不足。";
                case "1305":
                case "429":
                    return "请求过于频繁，请增大“请求间隔”后重试。";
                case "404":
                    return "接口地址(BaseUrl)或模型名(Model)不正确。";
                default:
                    return String.IsNullOrWhiteSpace(fallback) ? "未知错误。" : fallback;
            }
        }

        private static object[] AsArray(object value)
        {
            var array = value as object[];
            if (array != null)
            {
                return array;
            }
            var list = value as ArrayList;
            return list == null ? null : list.ToArray();
        }

        private static Dictionary<string, object> GetDict(Dictionary<string, object> parent, string key)
        {
            object value;
            return parent.TryGetValue(key, out value) ? value as Dictionary<string, object> : null;
        }
    }
}

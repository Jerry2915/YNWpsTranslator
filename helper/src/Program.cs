using System;
using System.IO;
using System.Net;
using System.Threading;

namespace YNWpsTranslatorHelper
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            string dataDirectory = GetDataDirectory(args);
            Directory.CreateDirectory(dataDirectory);
            Log.Initialize(Path.Combine(dataDirectory, "helper.log"));

            bool createdNew;
            using (var mutex = new Mutex(true, "Local\\YNWpsTranslatorHelper_17653", out createdNew))
            {
                if (!createdNew)
                {
                    return;
                }

                try
                {
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                    var service = new TranslationService(dataDirectory);
                    using (var server = new LocalHttpServer(IPAddress.Loopback, 17653, service))
                    {
                        Log.Write("Helper started.");
                        server.Run();
                    }
                }
                catch (Exception ex)
                {
                    Log.Write("Fatal error: " + ex);
                }
            }
        }

        private static string GetDataDirectory(string[] args)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i].Equals("--data-dir", StringComparison.OrdinalIgnoreCase))
                {
                    return Path.GetFullPath(args[i + 1]);
                }
            }
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "YNWpsTranslator");
        }
    }
}

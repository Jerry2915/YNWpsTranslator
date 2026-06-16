using System;
using System.IO;
using System.Text;

namespace YNWpsTranslatorHelper
{
    internal static class Log
    {
        private static readonly object SyncRoot = new object();
        private static string _path;

        public static void Initialize(string path)
        {
            _path = path;
        }

        public static void Write(string message)
        {
            try
            {
                lock (SyncRoot)
                {
                    File.AppendAllText(
                        _path,
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + message + Environment.NewLine,
                        Encoding.UTF8);
                }
            }
            catch
            {
            }
        }
    }
}

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace YNWpsTranslatorHelper
{
    internal sealed class SettingsStore
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("YNWpsTranslator.Settings.v1");
        private readonly string _path;
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer();

        public SettingsStore(string dataDirectory)
        {
            _path = Path.Combine(dataDirectory, "settings.dat");
        }

        public AppSettings Load()
        {
            try
            {
                if (!File.Exists(_path))
                {
                    return new AppSettings();
                }
                var protectedBytes = File.ReadAllBytes(_path);
                var plainBytes = ProtectedData.Unprotect(
                    protectedBytes,
                    Entropy,
                    DataProtectionScope.CurrentUser);
                return _json.Deserialize<AppSettings>(Encoding.UTF8.GetString(plainBytes)) ?? new AppSettings();
            }
            catch (Exception ex)
            {
                Log.Write("Unable to load settings: " + ex.Message);
                return new AppSettings();
            }
        }

        public void Save(AppSettings settings)
        {
            var plainBytes = Encoding.UTF8.GetBytes(_json.Serialize(settings));
            var protectedBytes = ProtectedData.Protect(
                plainBytes,
                Entropy,
                DataProtectionScope.CurrentUser);
            var tempPath = _path + ".tmp";
            File.WriteAllBytes(tempPath, protectedBytes);
            if (File.Exists(_path))
            {
                File.Delete(_path);
            }
            File.Move(tempPath, _path);
        }
    }
}

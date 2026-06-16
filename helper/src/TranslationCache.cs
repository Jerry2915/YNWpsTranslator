using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace YNWpsTranslatorHelper
{
    internal sealed class TranslationCache
    {
        private readonly object _syncRoot = new object();
        private readonly string _path;
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer();
        private Dictionary<string, string> _entries;

        public TranslationCache(string dataDirectory)
        {
            _path = Path.Combine(dataDirectory, "cache.json");
            _entries = Load();
        }

        public bool TryGet(string key, out string value)
        {
            lock (_syncRoot)
            {
                return _entries.TryGetValue(key, out value);
            }
        }

        public void Set(string key, string value)
        {
            lock (_syncRoot)
            {
                _entries[key] = value;
            }
        }

        public void Save()
        {
            lock (_syncRoot)
            {
                File.WriteAllText(_path, _json.Serialize(_entries), new UTF8Encoding(false));
            }
        }

        public void Clear()
        {
            lock (_syncRoot)
            {
                _entries.Clear();
                Save();
            }
        }

        private Dictionary<string, string> Load()
        {
            try
            {
                if (!File.Exists(_path))
                {
                    return new Dictionary<string, string>();
                }
                return _json.Deserialize<Dictionary<string, string>>(File.ReadAllText(_path, Encoding.UTF8))
                    ?? new Dictionary<string, string>();
            }
            catch (Exception ex)
            {
                Log.Write("Unable to load cache: " + ex.Message);
                return new Dictionary<string, string>();
            }
        }
    }
}

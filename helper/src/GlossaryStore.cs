using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace YNWpsTranslatorHelper
{
    // 术语表按目标语种分别存储：glossary-id.txt（印尼语）、glossary-en.txt（英语）、
    // glossary-zh.txt（中文）。翻译时只加载当前目标语种对应的那份，避免不同语种术语互相串用。
    internal sealed class GlossaryStore
    {
        private readonly string _dir;
        private readonly string _legacyPath;

        public GlossaryStore(string dataDirectory)
        {
            _dir = dataDirectory;
            _legacyPath = Path.Combine(_dir, "glossary.txt");
            MigrateLegacy();
        }

        // 旧版只有一份 glossary.txt（默认就是中→印尼），迁移为印尼语术语表，避免丢数据。
        private void MigrateLegacy()
        {
            try
            {
                string idPath = PathFor("印尼语");
                if (File.Exists(_legacyPath) && !File.Exists(idPath))
                {
                    File.Copy(_legacyPath, idPath, false);
                }
            }
            catch (Exception ex)
            {
                Log.Write("Glossary migrate failed: " + ex.Message);
            }
        }

        private string PathFor(string lang)
        {
            string code;
            switch ((lang ?? "").Trim())
            {
                case "英语":
                case "英文":
                case "English":
                    code = "en";
                    break;
                case "中文":
                case "汉语":
                case "Chinese":
                    code = "zh";
                    break;
                case "印尼语":
                case "印度尼西亚语":
                case "Indonesian":
                    code = "id";
                    break;
                default:
                    code = "other";
                    break;
            }
            return Path.Combine(_dir, "glossary-" + code + ".txt");
        }

        public string ReadText(string lang)
        {
            string path = PathFor(lang);
            return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : "";
        }

        public Dictionary<string, string> ReadEntries(string lang)
        {
            var entries = new Dictionary<string, string>(StringComparer.Ordinal);
            var lines = ReadText(lang).Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#"))
                {
                    continue;
                }
                int separator = line.IndexOf('=');
                if (separator <= 0 || separator == line.Length - 1)
                {
                    continue;
                }
                var source = line.Substring(0, separator).Trim();
                var target = line.Substring(separator + 1).Trim();
                if (source.Length > 0 && target.Length > 0)
                {
                    entries[source] = target;
                }
            }
            return entries;
        }

        public int SaveText(string text, string lang)
        {
            File.WriteAllText(PathFor(lang), text ?? "", new UTF8Encoding(true));
            return ReadEntries(lang).Count;
        }

        public IList<KeyValuePair<string, string>> ReadEntriesLongestFirst(string lang)
        {
            return ReadEntries(lang)
                .OrderByDescending(pair => pair.Key.Length)
                .ToList();
        }
    }
}

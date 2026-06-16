using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace YNWpsTranslatorHelper
{
    internal sealed class TranslationService
    {
        // 同时发起的并发翻译数。GLM-4-Flash 免费档并发充足，6 路兼顾速度与限频风险。
        private const int MaxConcurrency = 6;

        private readonly SettingsStore _settingsStore;
        private readonly GlossaryStore _glossaryStore;
        private readonly TranslationCache _cache;
        private readonly LlmTranslator _translator = new LlmTranslator();

        public TranslationService(string dataDirectory)
        {
            _settingsStore = new SettingsStore(dataDirectory);
            _glossaryStore = new GlossaryStore(dataDirectory);
            _cache = new TranslationCache(dataDirectory);
        }

        public object GetSettings()
        {
            var settings = _settingsStore.Load();
            return new
            {
                ok = true,
                hasKey = !String.IsNullOrWhiteSpace(settings.ApiKey),
                baseUrl = settings.BaseUrl,
                model = settings.Model,
                targetLang = settings.TargetLang,
                intervalMs = settings.IntervalMs,
                replaceExistingBilingual = settings.ReplaceExistingBilingual,
                createBackup = settings.CreateBackup
            };
        }

        public object SaveSettings(Dictionary<string, object> body)
        {
            var settings = _settingsStore.Load();

            var apiKey = GetString(body, "apiKey");
            if (!String.IsNullOrWhiteSpace(apiKey))
            {
                settings.ApiKey = apiKey.Trim();
            }

            var baseUrl = GetString(body, "baseUrl").Trim();
            settings.BaseUrl = String.IsNullOrWhiteSpace(baseUrl) ? AppSettings.DefaultBaseUrl : baseUrl;

            var model = GetString(body, "model").Trim();
            settings.Model = String.IsNullOrWhiteSpace(model) ? AppSettings.DefaultModel : model;

            var targetLang = GetString(body, "targetLang").Trim();
            settings.TargetLang = String.IsNullOrWhiteSpace(targetLang) ? AppSettings.DefaultTargetLang : targetLang;

            settings.IntervalMs = Math.Max(0, Math.Min(10000, GetInt(body, "intervalMs", 300)));
            settings.ReplaceExistingBilingual = GetBool(body, "replaceExistingBilingual", settings.ReplaceExistingBilingual);
            settings.CreateBackup = GetBool(body, "createBackup", settings.CreateBackup);

            if (String.IsNullOrWhiteSpace(settings.ApiKey))
            {
                throw new UserVisibleException("请填写 API Key（智谱开放平台 个人中心 获取）。");
            }
            _settingsStore.Save(settings);
            return new { ok = true };
        }

        public object Test()
        {
            var settings = _settingsStore.Load();
            EnsureConfigured(settings);
            var translated = TranslateItem("上岗培训", _glossaryStore.ReadEntriesLongestFirst(settings.TargetLang), settings);
            return new { ok = true, translation = translated };
        }

        public object GetGlossary()
        {
            var settings = _settingsStore.Load();
            return new { ok = true, text = _glossaryStore.ReadText(settings.TargetLang), lang = settings.TargetLang };
        }

        public object SaveGlossary(Dictionary<string, object> body)
        {
            var settings = _settingsStore.Load();
            int count = _glossaryStore.SaveText(GetString(body, "text"), settings.TargetLang);
            _cache.Clear();
            return new { ok = true, count = count, lang = settings.TargetLang };
        }

        public object Translate(Dictionary<string, object> body)
        {
            var rawTexts = GetStringArray(body, "texts");
            if (rawTexts.Count == 0)
            {
                throw new UserVisibleException("没有收到需要翻译的文本。");
            }
            if (rawTexts.Count > 5000)
            {
                throw new UserVisibleException("单次翻译最多支持 5000 条唯一文本，请分批处理。");
            }

            // 去重并剔除空白，避免重复请求。
            var unique = new List<string>();
            var seen = new HashSet<string>();
            foreach (var t in rawTexts)
            {
                if (!String.IsNullOrWhiteSpace(t) && seen.Add(t))
                {
                    unique.Add(t);
                }
            }

            var settings = _settingsStore.Load();
            EnsureConfigured(settings);
            var glossary = _glossaryStore.ReadEntriesLongestFirst(settings.TargetLang);

            var translations = new ConcurrentDictionary<string, string>();
            var failures = new ConcurrentDictionary<string, string>();

            int nextIndex = -1;
            int aborted = 0;            // 0=继续，1=遇到鉴权/额度类致命错误，停止派发
            string abortMessage = null;

            int workerCount = Math.Min(MaxConcurrency, unique.Count);
            var threads = new Thread[workerCount];

            for (int w = 0; w < workerCount; w++)
            {
                threads[w] = new Thread(delegate ()
                {
                    while (Thread.VolatileRead(ref aborted) == 0)
                    {
                        int i = Interlocked.Increment(ref nextIndex);
                        if (i >= unique.Count)
                        {
                            break;
                        }
                        string source = unique[i];
                        try
                        {
                            translations[source] = TranslateItem(source, glossary, settings);
                        }
                        catch (UserVisibleException ex)
                        {
                            failures[source] = ex.Message;
                            if (IsCredentialOrQuotaError(ex.Message))
                            {
                                Interlocked.Exchange(ref abortMessage, ex.Message);
                                Interlocked.Exchange(ref aborted, 1);
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            failures[source] = ex.Message;
                        }
                    }
                });
                threads[w].IsBackground = true;
            }

            for (int w = 0; w < workerCount; w++)
            {
                threads[w].Start();
            }
            for (int w = 0; w < workerCount; w++)
            {
                threads[w].Join();
            }

            _cache.Save();

            // 鉴权/额度类错误属于配置问题，整批中止并把错误抛给用户。
            if (Thread.VolatileRead(ref aborted) == 1 && !String.IsNullOrEmpty(abortMessage))
            {
                throw new UserVisibleException(abortMessage);
            }

            var translationsOut = new Dictionary<string, string>(translations);
            var failuresOut = new Dictionary<string, string>(failures);
            return new
            {
                ok = true,
                translations = translationsOut,
                failedCount = failuresOut.Count,
                failures = failuresOut
            };
        }

        private string TranslateItem(string source, IList<KeyValuePair<string, string>> glossary, AppSettings settings)
        {
            // 缓存键纳入模型、目标语种与术语表，换模型或改术语后自动失效。
            string cacheKey = Hash(
                source + "\n" + settings.Model + "\n" + settings.TargetLang + "\n" + _glossaryStore.ReadText(settings.TargetLang));
            string cached;
            if (_cache.TryGet(cacheKey, out cached))
            {
                return cached;
            }

            string translated = _translator.Translate(source, glossary, settings).Trim();
            _cache.Set(cacheKey, translated);
            return translated;
        }

        private static void EnsureConfigured(AppSettings settings)
        {
            if (String.IsNullOrWhiteSpace(settings.ApiKey))
            {
                throw new UserVisibleException("尚未配置翻译 API，请先打开“API 设置”填写 API Key。");
            }
        }

        private static bool IsCredentialOrQuotaError(string message)
        {
            return message.IndexOf("鉴权", StringComparison.Ordinal) >= 0 ||
                message.IndexOf("API Key", StringComparison.OrdinalIgnoreCase) >= 0 ||
                message.IndexOf("额度", StringComparison.Ordinal) >= 0 ||
                message.IndexOf("余额", StringComparison.Ordinal) >= 0 ||
                message.IndexOf("频繁", StringComparison.Ordinal) >= 0 ||
                message.IndexOf("HTTP 401", StringComparison.Ordinal) >= 0 ||
                message.IndexOf("HTTP 429", StringComparison.Ordinal) >= 0 ||
                message.IndexOf("HTTP 404", StringComparison.Ordinal) >= 0;
        }

        private static string GetString(Dictionary<string, object> body, string key)
        {
            object value;
            return body.TryGetValue(key, out value) && value != null ? Convert.ToString(value) : "";
        }

        private static int GetInt(Dictionary<string, object> body, string key, int defaultValue)
        {
            object value;
            int result;
            return body.TryGetValue(key, out value) &&
                Int32.TryParse(Convert.ToString(value), out result)
                ? result
                : defaultValue;
        }

        private static bool GetBool(Dictionary<string, object> body, string key, bool defaultValue)
        {
            object value;
            bool result;
            return body.TryGetValue(key, out value) &&
                Boolean.TryParse(Convert.ToString(value), out result)
                ? result
                : defaultValue;
        }

        private static IList<string> GetStringArray(Dictionary<string, object> body, string key)
        {
            var result = new List<string>();
            object raw;
            if (!body.TryGetValue(key, out raw) || raw == null)
            {
                return result;
            }
            var array = raw as object[];
            if (array != null)
            {
                foreach (var item in array)
                {
                    result.Add(Convert.ToString(item));
                }
                return result;
            }
            var list = raw as ArrayList;
            if (list != null)
            {
                foreach (var item in list)
                {
                    result.Add(Convert.ToString(item));
                }
            }
            return result;
        }

        private static string Hash(string value)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
                var output = new StringBuilder(bytes.Length * 2);
                foreach (var b in bytes)
                {
                    output.Append(b.ToString("x2"));
                }
                return output.ToString();
            }
        }
    }
}

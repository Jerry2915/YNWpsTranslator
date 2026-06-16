namespace YNWpsTranslatorHelper
{
    internal sealed class AppSettings
    {
        // 兼容 OpenAI 格式的对话补全接口（智谱 GLM 默认）。
        public string ApiKey { get; set; }
        public string BaseUrl { get; set; }
        public string Model { get; set; }
        public string TargetLang { get; set; }
        public int IntervalMs { get; set; }
        public bool ReplaceExistingBilingual { get; set; }
        public bool CreateBackup { get; set; }

        public const string DefaultBaseUrl = "https://open.bigmodel.cn/api/paas/v4/chat/completions";
        public const string DefaultModel = "glm-4-flash";
        public const string DefaultTargetLang = "印尼语";

        public AppSettings()
        {
            ApiKey = "";
            BaseUrl = DefaultBaseUrl;
            Model = DefaultModel;
            TargetLang = DefaultTargetLang;
            IntervalMs = 300;
            ReplaceExistingBilingual = true;
            CreateBackup = true;
        }
    }
}

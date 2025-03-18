namespace SmartSpeaker.Core.Config
{
    /// <summary>
    /// Azure 服务配置
    /// </summary>
    public class AzureConfig
    {
        /// <summary>
        /// Azure 语音服务API Key
        /// </summary>
        public string SpeechKey { get; set; } = string.Empty;
        
        /// <summary>
        /// Azure 语音服务区域
        /// </summary>
        public string SpeechRegion { get; set; } = string.Empty;
        
        /// <summary>
        /// 语音识别语言
        /// </summary>
        public string RecognitionLanguage { get; set; } = "zh-CN";
        
        /// <summary>
        /// 语音合成语言
        /// </summary>
        public string SynthesisLanguage { get; set; } = "zh-CN";
        
        /// <summary>
        /// 语音合成声音名称
        /// </summary>
        public string VoiceName { get; set; } = "zh-CN-XiaoxiaoNeural";
        
        /// <summary>
        /// 唤醒词模型ID
        /// </summary>
        public string KeywordModelId { get; set; } = string.Empty;
        
        /// <summary>
        /// 唤醒词
        /// </summary>
        public string Keyword { get; set; } = "小智小智";
    }
} 
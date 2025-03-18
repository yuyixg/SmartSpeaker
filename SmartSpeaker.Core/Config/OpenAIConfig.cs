namespace SmartSpeaker.Core.Config
{
    /// <summary>
    /// OpenAI API配置
    /// </summary>
    public class OpenAIConfig
    {
        /// <summary>
        /// API Key
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;
        
        /// <summary>
        /// API 基础URL
        /// </summary>
        public string BaseUrl { get; set; } = "https://api.openai.com/v1";
        
        /// <summary>
        /// 模型名称
        /// </summary>
        public string Model { get; set; } = "gpt-3.5-turbo";
        
        /// <summary>
        /// 系统提示语
        /// </summary>
        public string SystemPrompt { get; set; } = "你是小智，一个智能音箱助手。请简明扼要地回答用户的问题。对于音箱无法完成的功能，请诚实地告知用户。";
        
        /// <summary>
        /// 最大Token数量
        /// </summary>
        public int MaxTokens { get; set; } = 100;
        
        /// <summary>
        /// 温度参数 (0-1)
        /// </summary>
        public double Temperature { get; set; } = 0.7;
    }
} 
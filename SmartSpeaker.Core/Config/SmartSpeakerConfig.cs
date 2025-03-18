namespace SmartSpeaker.Core.Config
{
    /// <summary>
    /// 智能音箱配置
    /// </summary>
    public class SmartSpeakerConfig
    {
        /// <summary>
        /// 唤醒提示音文件路径
        /// </summary>
        public string WakeSound { get; set; } = string.Empty;
        
        /// <summary>
        /// 对话超时时间（毫秒）
        /// </summary>
        public int ListeningTimeout { get; set; } = 10000;
        
        /// <summary>
        /// 最大对话历史记录数量（轮次）
        /// </summary>
        public int MaxConversationHistory { get; set; } = 5;
    }
} 
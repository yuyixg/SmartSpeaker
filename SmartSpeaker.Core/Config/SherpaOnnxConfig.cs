namespace SmartSpeaker.Core.Config
{
    /// <summary>
    /// sherpa-onnx配置类
    /// </summary>
    public class SherpaOnnxConfig
    {
        /// <summary>
        /// 模型目录
        /// </summary>
        public string ModelDir { get; set; } = "Models/sherpa-onnx-kws";

        /// <summary>
        /// 编码器模型文件名
        /// </summary>
        public string EncoderModel { get; set; } = "encoder-epoch-12-avg-2-chunk-16-left-64.onnx";

        /// <summary>
        /// 解码器模型文件名
        /// </summary>
        public string DecoderModel { get; set; } = "decoder-epoch-12-avg-2-chunk-16-left-64.onnx";

        /// <summary>
        /// 连接器模型文件名
        /// </summary>
        public string JoinerModel { get; set; } = "joiner-epoch-12-avg-2-chunk-16-left-64.onnx";

        /// <summary>
        /// Paraformer编码器模型文件名
        /// </summary>
        public string ParaformerEncoder { get; set; } = "";

        /// <summary>
        /// Paraformer解码器模型文件名
        /// </summary>
        public string ParaformerDecoder { get; set; } = "";

        /// <summary>
        /// 词表文件名
        /// </summary>
        public string TokensFile { get; set; } = "tokens.txt";

        /// <summary>
        /// 关键词文件名
        /// </summary>
        public string KeywordsFile { get; set; } = "keywords.txt";

        /// <summary>
        /// 关键词列表
        /// </summary>
        public string[] Keywords { get; set; } = Array.Empty<string>();

        /// <summary>
        /// 模型提供程序 (cpu 或 cuda)
        /// </summary>
        public string Provider { get; set; } = "cpu";

        /// <summary>
        /// 使用的线程数
        /// </summary>
        public int NumThreads { get; set; } = 1;

        /// <summary>
        /// 采样率
        /// </summary>
        public int SampleRate { get; set; } = 16000;

        /// <summary>
        /// 特征维度
        /// </summary>
        public int FeatureDim { get; set; } = 80;

        /// <summary>
        /// 是否启用调试模式
        /// </summary>
        public bool Debug { get; set; } = false;
        
        /// <summary>
        /// 唤醒词
        /// </summary>
        public string Keyword { get; set; } = "小智小智";
        
        /// <summary>
        /// 唤醒词分数阈值
        /// </summary>
        public float WakeWordThreshold { get; set; } = 0.5f;
    }
} 
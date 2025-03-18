namespace SmartSpeaker.Core.Config
{
    /// <summary>
    /// sherpa-onnx语音识别配置类
    /// </summary>
    public class SherpaOnnxSpeechConfig
    {
        /// <summary>
        /// 模型目录
        /// </summary>
        public string ModelDir { get; set; } = "Models/sherpa-onnx-speech";

        /// <summary>
        /// 转录器编码器模型文件名
        /// </summary>
        public string EncoderModel { get; set; } = "encoder.onnx";

        /// <summary>
        /// 转录器解码器模型文件名
        /// </summary>
        public string DecoderModel { get; set; } = "decoder.onnx";

        /// <summary>
        /// 转录器连接器模型文件名
        /// </summary>
        public string JoinerModel { get; set; } = "joiner.onnx";

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
        /// 模型提供程序 (cpu 或 cuda)
        /// </summary>
        public string Provider { get; set; } = "cpu";

        /// <summary>
        /// 使用的线程数
        /// </summary>
        public int NumThreads { get; set; } = 2;

        /// <summary>
        /// 采样率
        /// </summary>
        public int SampleRate { get; set; } = 16000;

        /// <summary>
        /// 特征维度
        /// </summary>
        public int FeatureDim { get; set; } = 80;

        /// <summary>
        /// 解码方法，greedy_search 或 modified_beam_search
        /// </summary>
        public string DecodingMethod { get; set; } = "greedy_search";

        /// <summary>
        /// 最大活动路径数
        /// </summary>
        public int MaxActivePaths { get; set; } = 4;

        /// <summary>
        /// 是否启用端点检测
        /// </summary>
        public bool EnableEndpoint { get; set; } = false;

        /// <summary>
        /// 规则1最小尾随静音时间（秒）
        /// </summary>
        public float Rule1MinTrailingSilence { get; set; } = 2.4f;

        /// <summary>
        /// 规则2最小尾随静音时间（秒）
        /// </summary>
        public float Rule2MinTrailingSilence { get; set; } = 1.2f;

        /// <summary>
        /// 规则3最小话语长度（秒）
        /// </summary>
        public float Rule3MinUtteranceLength { get; set; } = 20.0f;

        /// <summary>
        /// 静音超时时间（秒）
        /// </summary>
        public float SilenceTimeoutSeconds { get; set; } = 2.0f;
    }
} 
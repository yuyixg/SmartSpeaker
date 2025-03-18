using System;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.IO;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using SmartSpeaker.Core.Config;
using SmartSpeaker.Core.Interfaces;
using SherpaOnnx;
using PortAudioSharp;
using Stream = PortAudioSharp.Stream;

namespace SmartSpeaker.Core.Services
{
    /// <summary>
    /// 使用sherpa-onnx的唤醒词检测器
    /// </summary>
    public class SherpaOnnxWakeWordDetector : IWakeWordDetector, IDisposable
    {
        private readonly ILogger<SherpaOnnxWakeWordDetector> _logger;
        private readonly SherpaOnnxConfig _config;
        private readonly string _wakeWord;
        private KeywordSpotter? _keywordSpotter;
        private dynamic? _stream; // Use dynamic type to resolve method calls at runtime
        private bool _isRunning;
        private bool _isPaused;
        private CancellationTokenSource? _cts;

        /// <summary>
        /// 唤醒词检测到事件
        /// </summary>
        public event Action<string>? OnWakeWordDetected;

        /// <summary>
        /// 初始化sherpa-onnx唤醒词检测器
        /// </summary>
        /// <param name="logger">日志记录器</param>
        /// <param name="config">sherpa-onnx配置</param>
        public SherpaOnnxWakeWordDetector(
            ILogger<SherpaOnnxWakeWordDetector> logger,
            IOptions<SherpaOnnxConfig> config)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
            _wakeWord = _config.Keyword;

            try
            {
                InitializeKeywordSpotter();
                _logger.LogDebug("sherpa-onnx唤醒词检测器已初始化");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"初始化sherpa-onnx唤醒词检测器时发生错误: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 初始化关键词检测器
        /// </summary>
        private void InitializeKeywordSpotter()
        {
            var config = new KeywordSpotterConfig();
            
            // 设置特征配置
            config.FeatConfig.SampleRate = _config.SampleRate;
            config.FeatConfig.FeatureDim = _config.FeatureDim;

            // 设置模型路径
            var modelDir = Path.GetFullPath(_config.ModelDir);
            
            _logger.LogInformation($"使用模型目录: {modelDir}");
            
            if (!Directory.Exists(modelDir))
            {
                throw new DirectoryNotFoundException($"模型目录不存在: {modelDir}");
            }
            
            var encoderPath = Path.Combine(modelDir, _config.EncoderModel);
            var decoderPath = Path.Combine(modelDir, _config.DecoderModel);
            var joinerPath = Path.Combine(modelDir, _config.JoinerModel);
            
            if (!File.Exists(encoderPath))
            {
                throw new FileNotFoundException($"编码器模型文件不存在: {encoderPath}");
            }
            
            if (!File.Exists(decoderPath))
            {
                throw new FileNotFoundException($"解码器模型文件不存在: {decoderPath}");
            }
            
            if (!File.Exists(joinerPath))
            {
                throw new FileNotFoundException($"连接器模型文件不存在: {joinerPath}");
            }
            
            config.ModelConfig.Transducer.Encoder = encoderPath;
            config.ModelConfig.Transducer.Decoder = decoderPath;
            config.ModelConfig.Transducer.Joiner = joinerPath;
            
            _logger.LogInformation($"使用模型: Encoder={encoderPath}, Decoder={decoderPath}, Joiner={joinerPath}");
            
            // 设置词表
            var tokensPath = Path.Combine(modelDir, _config.TokensFile);
            if (!File.Exists(tokensPath))
            {
                throw new FileNotFoundException($"词表文件不存在: {tokensPath}");
            }
            
            config.ModelConfig.Tokens = tokensPath;
            _logger.LogInformation($"使用词表文件: {tokensPath}");
            
            // 如果关键词文件存在，则使用文件中的关键词
            string keywordsFile = Path.Combine(modelDir, _config.KeywordsFile);
            if (File.Exists(keywordsFile))
            {
                config.KeywordsFile = keywordsFile;
                _logger.LogInformation($"使用关键词文件: {keywordsFile}");
            }
            else
            {
                _logger.LogWarning($"关键词文件不存在: {keywordsFile}，将使用配置中的关键词");
            }

            // 设置其他配置
            config.ModelConfig.Provider = _config.Provider;
            config.ModelConfig.NumThreads = _config.NumThreads;

            try
            {
                _logger.LogInformation("开始初始化sherpa-onnx关键词检测器");
                _keywordSpotter = new KeywordSpotter(config);
                _logger.LogDebug("sherpa-onnx关键词检测器初始化成功");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"sherpa-onnx关键词检测器初始化失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 启动唤醒词检测
        /// </summary>
        public void Start()
        {
            if (_isRunning)
            {
                _logger.LogWarning("唤醒词检测器已经在运行中");
                return;
            }

            try
            {
                _logger.LogInformation("启动sherpa-onnx唤醒词检测");

                // 创建取消标记源
                _cts = new CancellationTokenSource();

                // 启动关键词检测任务
                StartKeywordDetectionAsync(_cts.Token);

                _isRunning = true;
                _isPaused = false;

                _logger.LogInformation("sherpa-onnx唤醒词检测已启动");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"启动sherpa-onnx唤醒词检测时发生错误: {ex.Message}");
                Cleanup();
            }
        }

        /// <summary>
        /// 停止唤醒词检测
        /// </summary>
        public void Stop()
        {
            if (!_isRunning)
            {
                _logger.LogWarning("唤醒词检测器已经是停止状态");
                return;
            }

            try
            {
                _logger.LogInformation("停止sherpa-onnx唤醒词检测");

                // 取消正在进行的任务
                _cts?.Cancel();
                Cleanup();

                _isRunning = false;
                _isPaused = false;

                _logger.LogInformation("sherpa-onnx唤醒词检测已停止");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"停止sherpa-onnx唤醒词检测时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 暂停唤醒词检测
        /// </summary>
        public void Pause()
        {
            if (!_isRunning || _isPaused)
            {
                return;
            }

            try
            {
                _logger.LogDebug("暂停sherpa-onnx唤醒词检测");
                _isPaused = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"暂停sherpa-onnx唤醒词检测时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 恢复唤醒词检测
        /// </summary>
        public void Resume()
        {
            if (!_isRunning || !_isPaused)
            {
                return;
            }

            try
            {
                _logger.LogDebug("恢复sherpa-onnx唤醒词检测");
                _isPaused = false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"恢复sherpa-onnx唤醒词检测时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 启动关键词检测任务
        /// </summary>
        /// <param name="cancellationToken">取消标记</param>
        private async void StartKeywordDetectionAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogDebug("开始sherpa-onnx唤醒词检测任务");

                // 创建音频流，如果有自定义关键词，则在创建流时添加
                if (_config.Keywords != null && _config.Keywords.Length > 0)
                {
                    string keywords = string.Join(";", _config.Keywords);
                    _stream = _keywordSpotter?.CreateStream(keywords);
                }
                else if (!string.IsNullOrEmpty(_config.Keyword))
                {
                    _stream = _keywordSpotter?.CreateStream(_config.Keyword);
                }
                else
                {
                    _stream = _keywordSpotter?.CreateStream();
                }

                // 初始化音频捕获
                using (var recorder = new PortAudioRecorder(_config.SampleRate, 1))
                {
                    recorder.Start();

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        if (_isPaused)
                        {
                            await Task.Delay(100, cancellationToken);
                            continue;
                        }

                        try
                        {
                            // 读取音频数据
                            float[] samples = recorder.ReadSamples(1024);
                            
                            // 将音频数据传递给关键词检测器
                            // 使用动态调用来支持不同类型的流
                            if (_stream != null)
                            {
                                try
                                {
                                    // 使用dynamic直接调用方法
                                    _stream.AcceptWaveform(_config.SampleRate, samples);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, $"调用AcceptWaveform方法出错: {ex.Message}");
                                }
                            }

                            // 检查是否有关键词可以解码
                            if (_stream != null && _keywordSpotter != null)
                            {
                                try 
                                {
                                    while (_keywordSpotter.IsReady(_stream))
                                    {
                                        _keywordSpotter.Decode(_stream);
                                        var result = _keywordSpotter.GetResult(_stream);
                                        
                                        // 如果检测到关键词
                                        if (!string.IsNullOrEmpty(result.Keyword))
                                        {
                                            // 必须在检测到关键词后重置流，否则会一直触发
                                            _keywordSpotter.Reset(_stream);
                                            
                                            _logger.LogDebug($"检测到唤醒词: {result.Keyword}");
                                            
                                            // 如果检测到的是目标唤醒词，触发事件
                                          //  if (_wakeWord.Equals(result.Keyword, StringComparison.OrdinalIgnoreCase))
                                            {
                                                OnWakeWordDetected?.Invoke(_wakeWord);
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, $"处理唤醒词检测时出错: {ex.Message}");
                                }
                            }

                            // 避免CPU使用率过高
                            await Task.Delay(10, cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"sherpa-onnx唤醒词检测过程中发生错误: {ex.Message}");
                            await Task.Delay(1000, cancellationToken); // 错误后短暂延迟
                        }
                    }
                    
                    recorder.Stop();
                }
            }
            catch (TaskCanceledException)
            {
                _logger.LogInformation("sherpa-onnx唤醒词检测任务已取消");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"sherpa-onnx唤醒词检测任务中发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        private void Cleanup()
        {
            try
            {
                // 重置流
                _stream = null;
                
                if (_cts != null)
                {
                    _cts.Dispose();
                    _cts = null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"清理资源时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _logger.LogDebug("正在释放SherpaOnnxWakeWordDetector资源");

            Stop();
            Cleanup();

            _logger.LogDebug("SherpaOnnxWakeWordDetector资源已释放");
        }

        /// <summary>
        /// 简单的PortAudio录音机实现
        /// </summary>
        private class PortAudioRecorder : IDisposable
        {
            private readonly Stream? _stream;
            private readonly float[] _buffer;
            private int _bufferPos;
            private int _bufferAvailable;
            private readonly object _bufferLock = new object();

            public PortAudioRecorder(int sampleRate, int channelCount)
            {
                // 初始化PortAudio
                PortAudio.Initialize();

                // 使用默认输入设备
                int deviceIndex = PortAudio.DefaultInputDevice;
                if (deviceIndex == PortAudio.NoDevice)
                {
                    throw new Exception("找不到默认输入设备");
                }

                var deviceInfo = PortAudio.GetDeviceInfo(deviceIndex);

                // 设置流参数
                var param = new StreamParameters
                {
                    device = deviceIndex,
                    channelCount = channelCount,
                    sampleFormat = SampleFormat.Float32,
                    suggestedLatency = deviceInfo.defaultLowInputLatency
                };

                // 创建缓冲区
                _buffer = new float[8192]; // 足够大的缓冲区
                _bufferPos = 0;
                _bufferAvailable = 0;

                // 创建回调
                Stream.Callback callback = (IntPtr input, IntPtr output,
                    uint frameCount,
                    ref StreamCallbackTimeInfo timeInfo,
                    StreamCallbackFlags statusFlags,
                    IntPtr userData) =>
                {
                    if (input != IntPtr.Zero)
                    {
                        try
                        {
                            // 确保帧数不超过预期，并处理可能的溢出
                            int safeFrameCount = (int)Math.Min(frameCount, 4096); // 限制最大帧数
                            
                            if (safeFrameCount <= 0)
                            {
                                return StreamCallbackResult.Continue;
                            }

                            // 从输入缓冲区读取音频数据
                            float[] samples = new float[safeFrameCount];
                            Marshal.Copy(input, samples, 0, safeFrameCount);

                            // 将音频数据复制到缓冲区
                            lock (_bufferLock)
                            {
                                // 如果缓冲区已满，丢弃最旧的数据
                                if (_bufferAvailable + safeFrameCount > _buffer.Length)
                                {
                                    int discardCount = _bufferAvailable + safeFrameCount - _buffer.Length;
                                    Array.Copy(_buffer, discardCount, _buffer, 0, _bufferAvailable - discardCount);
                                    _bufferAvailable -= discardCount;
                                }

                                // 复制新数据到缓冲区
                                Array.Copy(samples, 0, _buffer, _bufferAvailable, safeFrameCount);
                                _bufferAvailable += safeFrameCount;
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"处理音频数据时发生错误: {ex.Message}");
                        }
                    }

                    return StreamCallbackResult.Continue;
                };

                // 创建音频流
                _stream = new Stream(param, null, sampleRate, 1024, StreamFlags.NoFlag, callback, IntPtr.Zero);
            }

            public void Start()
            {
                _stream?.Start();
            }

            public void Stop()
            {
                _stream?.Stop();
            }

            public float[] ReadSamples(int count)
            {
                lock (_bufferLock)
                {
                    // 如果缓冲区中没有足够的数据，返回空数组
                    if (_bufferAvailable < count)
                    {
                        return new float[0];
                    }

                    // 从缓冲区读取数据
                    float[] result = new float[count];
                    Array.Copy(_buffer, 0, result, 0, count);
                    Array.Copy(_buffer, count, _buffer, 0, _bufferAvailable - count);
                    _bufferAvailable -= count;

                    return result;
                }
            }

            public void Dispose()
            {
                _stream?.Dispose();
            }
        }
    }
} 
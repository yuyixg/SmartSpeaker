using System;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
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
    /// 使用sherpa-onnx的语音录制器
    /// </summary>
    public class SherpaOnnxSpeechRecorder : ISpeechRecorder, IDisposable
    {
        private readonly ILogger<SherpaOnnxSpeechRecorder> _logger;
        private readonly SherpaOnnxSpeechConfig _config;
        private readonly TimeSpan _silenceTimeout;
        private OnlineRecognizer? _recognizer;
        private OnlineStream? _recognizerStream;
        private Stream? _audioStream;
        private CancellationTokenSource? _cts;
        private TaskCompletionSource<bool>? _recordingTaskSource;
        private DateTime _lastSpeechTime;
        private string _lastText = string.Empty;
        private bool _isRecording;
        private string _currentText = string.Empty;
        private bool _hasVoiceActivity;
        private const float _energyThreshold = 0.01f;

        /// <summary>
        /// 语音识别事件
        /// </summary>
        public event Action<string>? OnSpeechRecognized;

        /// <summary>
        /// 录音开始事件
        /// </summary>
        public event Action? OnRecordingStarted;

        /// <summary>
        /// 录音停止事件
        /// </summary>
        public event Action? OnRecordingStopped;

        /// <summary>
        /// 是否正在录音
        /// </summary>
        public bool IsRecording => _isRecording;

        /// <summary>
        /// 初始化sherpa-onnx语音录制器
        /// </summary>
        /// <param name="logger">日志记录器</param>
        /// <param name="config">sherpa-onnx语音识别配置</param>
        public SherpaOnnxSpeechRecorder(
            ILogger<SherpaOnnxSpeechRecorder> logger,
            IOptions<SherpaOnnxSpeechConfig> config)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
            _silenceTimeout = TimeSpan.FromSeconds(_config.SilenceTimeoutSeconds);

            try
            {
                InitializeRecognizer();
                _logger.LogDebug("sherpa-onnx语音录制器已初始化");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"初始化sherpa-onnx语音录制器时发生错误: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 初始化sherpa-onnx识别器
        /// </summary>
        private void InitializeRecognizer()
        {
            var config = new OnlineRecognizerConfig();

            // 设置特征配置
            config.FeatConfig.SampleRate = _config.SampleRate;
            config.FeatConfig.FeatureDim = _config.FeatureDim;

            // 设置模型路径
            var modelDir = System.IO.Path.GetFullPath(_config.ModelDir);

            _logger.LogInformation($"使用模型目录: {modelDir}");

            if (!System.IO.Directory.Exists(modelDir))
            {
                throw new System.IO.DirectoryNotFoundException($"模型目录不存在: {modelDir}");
            }

            // 设置转录器模型（如果使用）
            if (!string.IsNullOrEmpty(_config.EncoderModel) &&
                !string.IsNullOrEmpty(_config.DecoderModel) &&
                !string.IsNullOrEmpty(_config.JoinerModel))
            {
                var encoderPath = System.IO.Path.Combine(modelDir, _config.EncoderModel);
                var decoderPath = System.IO.Path.Combine(modelDir, _config.DecoderModel);
                var joinerPath = System.IO.Path.Combine(modelDir, _config.JoinerModel);

                if (!System.IO.File.Exists(encoderPath))
                {
                    throw new System.IO.FileNotFoundException($"编码器模型文件不存在: {encoderPath}");
                }

                if (!System.IO.File.Exists(decoderPath))
                {
                    throw new System.IO.FileNotFoundException($"解码器模型文件不存在: {decoderPath}");
                }

                if (!System.IO.File.Exists(joinerPath))
                {
                    throw new System.IO.FileNotFoundException($"连接器模型文件不存在: {joinerPath}");
                }

                config.ModelConfig.Transducer.Encoder = encoderPath;
                config.ModelConfig.Transducer.Decoder = decoderPath;
                config.ModelConfig.Transducer.Joiner = joinerPath;

                _logger.LogInformation($"使用转录器模型: Encoder={encoderPath}, Decoder={decoderPath}, Joiner={joinerPath}");
            }

            // 设置Paraformer模型（如果使用）
            if (!string.IsNullOrEmpty(_config.ParaformerEncoder) &&
                !string.IsNullOrEmpty(_config.ParaformerDecoder))
            {
                var encoderPath = System.IO.Path.Combine(modelDir, _config.ParaformerEncoder);
                var decoderPath = System.IO.Path.Combine(modelDir, _config.ParaformerDecoder);

                if (!System.IO.File.Exists(encoderPath))
                {
                    throw new System.IO.FileNotFoundException($"Paraformer编码器模型文件不存在: {encoderPath}");
                }

                if (!System.IO.File.Exists(decoderPath))
                {
                    throw new System.IO.FileNotFoundException($"Paraformer解码器模型文件不存在: {decoderPath}");
                }

                config.ModelConfig.Paraformer.Encoder = encoderPath;
                config.ModelConfig.Paraformer.Decoder = decoderPath;

                _logger.LogInformation($"使用Paraformer模型: Encoder={encoderPath}, Decoder={decoderPath}");
            }

            // 设置词表
            var tokensPath = System.IO.Path.Combine(modelDir, _config.TokensFile);
            if (!System.IO.File.Exists(tokensPath))
            {
                throw new System.IO.FileNotFoundException($"词表文件不存在: {tokensPath}");
            }

            config.ModelConfig.Tokens = tokensPath;
            _logger.LogInformation($"使用词表文件: {tokensPath}");

            // 设置其他配置
            config.ModelConfig.Provider = _config.Provider;
            config.ModelConfig.NumThreads = _config.NumThreads;
            config.DecodingMethod = _config.DecodingMethod;
            config.MaxActivePaths = _config.MaxActivePaths;

            // 设置端点检测
            config.EnableEndpoint = 1;
            config.Rule1MinTrailingSilence = _config.Rule1MinTrailingSilence;
            config.Rule2MinTrailingSilence = _config.Rule2MinTrailingSilence;
            config.Rule3MinUtteranceLength = _config.Rule3MinUtteranceLength;

            try
            {
                _logger.LogInformation("开始初始化sherpa-onnx语音识别器");
                _recognizer = new OnlineRecognizer(config);
                _recognizerStream = _recognizer.CreateStream();
                _logger.LogDebug("sherpa-onnx语音识别器初始化成功");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"sherpa-onnx语音识别器初始化失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 初始化音频捕获
        /// </summary>
        private void InitializeAudioCapture()
        {
            try
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
                _logger.LogDebug($"使用音频设备: {deviceInfo.name}");

                // 设置流参数
                var param = new StreamParameters
                {
                    device = deviceIndex,
                    channelCount = 1,
                    sampleFormat = SampleFormat.Float32,
                    suggestedLatency = deviceInfo.defaultLowInputLatency
                };

                // 创建回调
                Stream.Callback callback = (IntPtr input, IntPtr output,
                    uint frameCount,
                    ref StreamCallbackTimeInfo timeInfo,
                    StreamCallbackFlags statusFlags,
                    IntPtr userData) =>
                {
                    // 增强安全检查，确保输入有效且录音状态正确
                    if (!_isRecording || input == IntPtr.Zero)
                    {
                        return StreamCallbackResult.Continue;
                    }

                    // 创建本地引用，避免在处理过程中资源被释放
                    OnlineStream? localStream = null;
                    OnlineRecognizer? localRecognizer = null;
                    
                    // 使用锁确保不会在资源释放过程中访问它们
                    lock (_recordingLock)
                    {
                        // 在锁内再次检查资源是否有效
                        if (_recognizerStream == null || _recognizer == null || !_isRecording)
                        {
                            return StreamCallbackResult.Continue;
                        }
                        
                        // 在锁内获取本地引用
                        localStream = _recognizerStream;
                        localRecognizer = _recognizer;
                    }

                    // 再次确认本地引用有效
                    if (localStream == null || localRecognizer == null)
                    {
                        return StreamCallbackResult.Continue;
                    }

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

                        // 检测音频能量
                        float energy = 0;
                        for (int i = 0; i < samples.Length; i++)
                        {
                            energy += samples[i] * samples[i];
                        }
                        energy /= samples.Length;
                        _hasVoiceActivity = energy > _energyThreshold;

                        // 再次检查录音状态，避免在处理过程中状态改变
                        if (!_isRecording)
                        {
                            return StreamCallbackResult.Continue;
                        }

                        // 将音频数据传递给识别器，使用本地引用
                        localStream.AcceptWaveform(_config.SampleRate, samples);

                        // 检查是否有结果可以解码
                        while (localRecognizer.IsReady(localStream))
                        {
                            localRecognizer.Decode(localStream);
                            var result = localRecognizer.GetResult(localStream);

                            if (!string.IsNullOrEmpty(result.Text))
                            {
                                _currentText = result.Text;
                                _logger.LogDebug($"识别到文本: {result.Text}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"处理音频数据时发生错误: {ex.Message}");
                    }


                    return StreamCallbackResult.Continue;
                };

                // 创建音频流
                _audioStream = new Stream(param, null, _config.SampleRate, 1024, StreamFlags.NoFlag, callback, IntPtr.Zero);

                // 启动音频流
                _audioStream.Start();
                _logger.LogDebug("音频捕获已初始化并启动");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"初始化音频捕获时发生错误: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 开始录音
        /// </summary>
        private readonly object _recordingLock = new object();
        public void StartRecording()
        {
            // 使用锁防止并发调用
            lock (_recordingLock)
            {
                if (_isRecording)
                {
                    _logger.LogWarning("语音录制已经在进行中");
                    return;
                }

                try
                {
                    _logger.LogInformation("开始sherpa-onnx语音录制");

                    // 创建取消标记源
                    _cts = new CancellationTokenSource();
                    _recordingTaskSource = new TaskCompletionSource<bool>();
                    _lastSpeechTime = DateTime.Now;
                    _lastText = string.Empty;
                    _currentText = string.Empty;

                    // 设置录音状态为true，防止回调函数中的竞态条件
                    _isRecording = true;

                    // 确保之前的流已经被释放
                    if (_recognizerStream != null)
                    {
                        try
                        {
                            _recognizerStream.Dispose();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"释放旧识别器流时发生错误: {ex.Message}");
                        }
                    }

                    // 初始化音频捕获
                    _recognizerStream = _recognizer.CreateStream();
                    InitializeAudioCapture();

                    // 启动静音检测
                    StartSilenceDetection();

                    OnRecordingStarted?.Invoke();

                    _logger.LogInformation("sherpa-onnx语音录制已开始");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"开始sherpa-onnx语音录制时发生错误: {ex.Message}");
                    _isRecording = false; // 确保在出错时重置状态
                    Cleanup();
                }
            }
        }

        /// <summary>
        /// 启动静音检测
        /// </summary>
        private void StartSilenceDetection()
        {
            Task.Run(async () =>
            {
                try
                {
                    while (!_cts?.Token.IsCancellationRequested ?? false)
                    {
                        if (_currentText == _lastText)
                        {
                            var silenceDuration = DateTime.Now - _lastSpeechTime;
                            if (silenceDuration >= _silenceTimeout)
                            {
                                _logger.LogInformation("检测到静音超时，停止录音");
                                // 确保在停止录音前触发语音识别事件

                                _logger.LogInformation($"触发语音识别事件: {_currentText}");
                                OnSpeechRecognized?.Invoke(_currentText);

                                StopRecording();
                                _lastText = string.Empty;
                                _currentText = string.Empty;
                                break;
                            }
                        }
                        else
                        {
                            _lastText = _currentText;
                            _lastSpeechTime = DateTime.Now;
                            _logger.LogDebug($"更新文本: {_currentText}");
                        }

                        await Task.Delay(100, _cts.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogDebug("静音检测已取消");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"静音检测过程中发生错误: {ex.Message}");
                }
            }, _cts?.Token ?? CancellationToken.None);
        }

        /// <summary>
        /// 停止录音
        /// </summary>
        public void StopRecording()
        {
            // 使用锁防止并发访问
            lock (_recordingLock)
            {
                if (!_isRecording)
                {
                    _logger.LogWarning("语音录制已经处于停止状态");
                    return;
                }

                try
                {
                    _logger.LogInformation("停止sherpa-onnx语音录制");

                    // 先设置录音状态为false，确保回调函数不再处理新的音频数据
                    _isRecording = false;

                    // 取消正在进行的任务
                    _cts?.Cancel();

                    // 停止音频流，确保不再有回调发生
                    if (_audioStream != null && !_audioStream.IsStopped)
                    {
                        try
                        {
                            _audioStream.Stop();
                            _logger.LogDebug("音频流已停止");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"停止音频流时发生错误: {ex.Message}");
                        }
                    }

                    // 等待一小段时间确保所有回调都已完成
                    Thread.Sleep(100);

                    // 重置识别器流，确保停止处理之前的音频数据
                    if (_recognizer != null && _recognizerStream != null)
                    {
                        try
                        {
                            // 释放旧的识别器流
                            _recognizerStream.Dispose();
                            _recognizerStream = null;
                            _logger.LogDebug("识别器流已释放");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"释放识别器流时发生错误: {ex.Message}");
                        }
                    }

                    // 清理音频资源
                    Cleanup();

                    // 清除文本内容，确保下次录音不会保留之前的内容
                    _lastText = string.Empty;
                    _currentText = string.Empty;

                    OnRecordingStopped?.Invoke();

                    _logger.LogInformation("sherpa-onnx语音录制已停止");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"停止sherpa-onnx语音录制时发生错误: {ex.Message}");
                    _isRecording = false;
                    OnRecordingStopped?.Invoke();

                    // 即使发生错误也清除文本内容
                    _lastText = string.Empty;
                    _currentText = string.Empty;
                }
            }
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        private void Cleanup()
        {
            try
            {
                // 停止音频流但不释放资源，保持单例状态
                if (_audioStream != null)
                {
                    try
                    {
                        // 检查音频流状态是否为活动状态，只有活动状态才能停止
                        if (!_audioStream.IsStopped)
                        {
                            _audioStream.Stop();
                            _logger.LogDebug("音频流已成功停止");
                        }
                        else
                        {
                            _logger.LogDebug("音频流已经处于停止状态，无需再次停止");
                        }

                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"停止音频流时发生错误: {ex.Message}");
                    }
                }

                // 释放取消标记源
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
            _logger.LogDebug("正在释放SherpaOnnxSpeechRecorder资源");

            if (_isRecording)
            {
                StopRecording();
            }

            Cleanup();

            // 在应用程序结束时释放所有资源
            if (_audioStream != null)
            {
                try
                {
                    _audioStream.Dispose();
                    _audioStream = null;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"释放音频流资源时发生错误: {ex.Message}");
                }
            }

            if (_recognizerStream != null)
            {
                try
                {
                    _recognizerStream.Dispose();
                    _recognizerStream = null;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"释放识别器流资源时发生错误: {ex.Message}");
                }
            }

            if (_recognizer != null)
            {
                try
                {
                    _recognizer.Dispose();
                    _recognizer = null;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"释放识别器资源时发生错误: {ex.Message}");
                }
            }

            _logger.LogDebug("SherpaOnnxSpeechRecorder资源已释放");
        }
    }
}
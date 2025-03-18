using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using SmartSpeaker.Core.Config;
using SmartSpeaker.Core.Interfaces;

namespace SmartSpeaker.Core.Services
{
    /// <summary>
    /// 使用Azure语音服务的语音录制器
    /// </summary>
    public class AzureSpeechRecorder : ISpeechRecorder, IDisposable
    {
        private readonly ILogger<AzureSpeechRecorder> _logger;
        private readonly AzureConfig _config;
        private SpeechRecognizer? _recognizer;
        private AudioConfig? _audioConfig;
        private SpeechConfig? _speechConfig;
        private bool _isRecording;
        private CancellationTokenSource? _cts;

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
        /// 初始化Azure语音录制器
        /// </summary>
        /// <param name="logger">日志记录器</param>
        /// <param name="config">Azure配置</param>
        public AzureSpeechRecorder(
            ILogger<AzureSpeechRecorder> logger,
            IOptions<AzureConfig> config)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = config?.Value ?? throw new ArgumentNullException(nameof(config));

            try
            {
                // 初始化语音配置
                _speechConfig = SpeechConfig.FromSubscription(_config.SpeechKey, _config.SpeechRegion);
                _speechConfig.SpeechRecognitionLanguage = _config.RecognitionLanguage;

                // 创建音频输入配置
                _audioConfig = AudioConfig.FromDefaultMicrophoneInput();

                _logger.LogDebug("Azure语音录制器已初始化");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"初始化Azure语音录制器时发生错误: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 开始录音
        /// </summary>
        public void StartRecording()
        {
            if (_isRecording || _speechConfig == null || _audioConfig == null)
            {
                _logger.LogWarning("语音录制已经在进行中或配置未初始化");
                return;
            }

            try
            {
                _logger.LogInformation("开始语音录制");

                // 创建取消标记源
                _cts = new CancellationTokenSource();

                // 创建语音识别器
                _recognizer = new SpeechRecognizer(_speechConfig, _audioConfig);

                // 注册识别事件
                _recognizer.Recognized += RecognizerOnRecognized;
                _recognizer.SessionStarted += RecognizerOnSessionStarted;
                _recognizer.SessionStopped += RecognizerOnSessionStopped;

                // 开始持续识别
                _recognizer.StartContinuousRecognitionAsync().GetAwaiter().GetResult();

                _isRecording = true;
                OnRecordingStarted?.Invoke();

                _logger.LogInformation("语音录制已开始");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"开始语音录制时发生错误: {ex.Message}");
                Cleanup();
            }
        }

        /// <summary>
        /// 停止录音
        /// </summary>
        public void StopRecording()
        {
            if (!_isRecording || _recognizer == null)
            {
                _logger.LogWarning("语音录制已经处于停止状态或识别器未初始化");
                return;
            }

            try
            {
                _logger.LogInformation("停止语音录制");

                // 停止连续识别
                _recognizer.StopContinuousRecognitionAsync().GetAwaiter().GetResult();

                // 取消正在进行的任务
                _cts?.Cancel();
                Cleanup();

                _isRecording = false;
                OnRecordingStopped?.Invoke();

                _logger.LogInformation("语音录制已停止");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"停止语音录制时发生错误: {ex.Message}");
                _isRecording = false;
                OnRecordingStopped?.Invoke();
            }
        }

        /// <summary>
        /// 语音识别事件处理
        /// </summary>
        private void RecognizerOnRecognized(object? sender, SpeechRecognitionEventArgs e)
        {
            if (e.Result.Reason == ResultReason.RecognizedSpeech && !string.IsNullOrWhiteSpace(e.Result.Text))
            {
                _logger.LogDebug($"识别到语音: {e.Result.Text}");
                OnSpeechRecognized?.Invoke(e.Result.Text);
            }
        }

        /// <summary>
        /// 会话开始事件处理
        /// </summary>
        private void RecognizerOnSessionStarted(object? sender, SessionEventArgs e)
        {
            _logger.LogDebug("语音识别会话已开始");
        }

        /// <summary>
        /// 会话结束事件处理
        /// </summary>
        private void RecognizerOnSessionStopped(object? sender, SessionEventArgs e)
        {
            _logger.LogDebug("语音识别会话已结束");
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        private void Cleanup()
        {
            try
            {
                if (_recognizer != null)
                {
                    _recognizer.Recognized -= RecognizerOnRecognized;
                    _recognizer.SessionStarted -= RecognizerOnSessionStarted;
                    _recognizer.SessionStopped -= RecognizerOnSessionStopped;
                    _recognizer.Dispose();
                    _recognizer = null;
                }

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
            _logger.LogDebug("正在释放AzureSpeechRecorder资源");

            if (_isRecording)
            {
                StopRecording();
            }

            Cleanup();
            
            _audioConfig?.Dispose();
            _audioConfig = null;
            
            // SpeechConfig 无需显式释放

            _logger.LogDebug("AzureSpeechRecorder资源已释放");
        }
    }
} 
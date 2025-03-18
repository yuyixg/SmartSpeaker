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
    /// 使用Azure语音服务的唤醒词检测器
    /// </summary>
    public class AzureWakeWordDetector : IWakeWordDetector, IDisposable
    {
        private readonly ILogger<AzureWakeWordDetector> _logger;
        private readonly AzureConfig _config;
        private SpeechRecognizer? _recognizer;
        private AudioConfig? _audioConfig;
        private SpeechConfig? _speechConfig;
        private KeywordRecognitionModel? _keywordModel;
        private bool _isRunning;
        private bool _isPaused;
        private CancellationTokenSource? _cts;

        /// <summary>
        /// 唤醒词检测到事件
        /// </summary>
        public event Action<string>? OnWakeWordDetected;

        /// <summary>
        /// 初始化Azure唤醒词检测器
        /// </summary>
        /// <param name="logger">日志记录器</param>
        /// <param name="config">Azure配置</param>
        public AzureWakeWordDetector(
            ILogger<AzureWakeWordDetector> logger,
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

                // 创建关键词模型
                if (!string.IsNullOrEmpty(_config.KeywordModelId))
                {
                    _keywordModel = KeywordRecognitionModel.FromFile(_config.KeywordModelId);
                }
                else
                {
                    _logger.LogWarning("未设置唤醒词模型ID，将使用默认唤醒词检测");
                }

                _logger.LogDebug("Azure唤醒词检测器已初始化");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"初始化Azure唤醒词检测器时发生错误: {ex.Message}");
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
                _logger.LogInformation("启动唤醒词检测");

                // 创建取消标记源
                _cts = new CancellationTokenSource();

                // 创建语音识别器
                if (_speechConfig == null || _audioConfig == null)
                {
                    _logger.LogError("语音配置或音频配置未初始化，无法启动唤醒词检测");
                    return;
                }
                
                _recognizer = new SpeechRecognizer(_speechConfig, _audioConfig);

                // 启动关键词检测任务
                StartKeywordRecognitionAsync(_cts.Token);

                _isRunning = true;
                _isPaused = false;

                _logger.LogInformation("唤醒词检测已启动");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"启动唤醒词检测时发生错误: {ex.Message}");
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
                _logger.LogInformation("停止唤醒词检测");

                // 取消正在进行的任务
                _cts?.Cancel();
                Cleanup();

                _isRunning = false;
                _isPaused = false;

                _logger.LogInformation("唤醒词检测已停止");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"停止唤醒词检测时发生错误: {ex.Message}");
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
                _logger.LogDebug("暂停唤醒词检测");
                _isPaused = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"暂停唤醒词检测时发生错误: {ex.Message}");
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
                _logger.LogDebug("恢复唤醒词检测");
                _isPaused = false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"恢复唤醒词检测时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 启动关键词识别任务
        /// </summary>
        /// <param name="cancellationToken">取消标记</param>
        private async void StartKeywordRecognitionAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogDebug("开始唤醒词识别任务");

                while (!cancellationToken.IsCancellationRequested)
                {
                    if (_isPaused)
                    {
                        await Task.Delay(100, cancellationToken);
                        continue;
                    }

                    try
                    {
                        if (_recognizer == null)
                        {
                            _logger.LogError("语音识别器未初始化");
                            break;
                        }
                        
                        // 使用关键词模型
                        if (_keywordModel != null)
                        {
                            _logger.LogDebug("等待唤醒词...");
                            // 使用KeywordRecognizer进行唤醒词检测
                            var keywordResult = await _recognizer.RecognizeOnceAsync();
                            
                            if (keywordResult.Reason == ResultReason.RecognizedSpeech)
                            {
                                var text = keywordResult.Text.Trim().ToLower();
                                _logger.LogDebug($"识别到文本: {text}");
                                
                                if (text.Contains(_config.Keyword.ToLower()))
                                {
                                    _logger.LogInformation("检测到唤醒词");
                                    OnWakeWordDetected?.Invoke(_config.Keyword);
                                }
                            }
                        }
                        // 使用普通识别
                        else
                        {
                            _logger.LogDebug("等待唤醒词（普通识别模式）...");
                            var result = await _recognizer.RecognizeOnceAsync();

                            if (result.Reason == ResultReason.RecognizedSpeech)
                            {
                                var text = result.Text.Trim().ToLower();
                                _logger.LogDebug($"识别到文本: {text}");

                                if (text.Contains(_config.Keyword.ToLower()))
                                {
                                    _logger.LogInformation("检测到唤醒词");
                                    OnWakeWordDetected?.Invoke(_config.Keyword);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"唤醒词识别过程中发生错误: {ex.Message}");
                        await Task.Delay(1000, cancellationToken); // 错误后短暂延迟
                    }

                    // 防止过度CPU使用
                    await Task.Delay(100, cancellationToken);
                }
            }
            catch (TaskCanceledException)
            {
                _logger.LogInformation("唤醒词识别任务已取消");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"唤醒词识别任务中发生错误: {ex.Message}");
            }
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
            _logger.LogDebug("正在释放AzureWakeWordDetector资源");

            Stop();
            
            _keywordModel?.Dispose();
            _keywordModel = null;
            
            _audioConfig?.Dispose();
            _audioConfig = null;
            
            // SpeechConfig 无需显式释放

            _logger.LogDebug("AzureWakeWordDetector资源已释放");
        }
    }
} 
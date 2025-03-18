using System;
using System.IO;
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
    /// 使用Azure语音服务的语音播放器
    /// </summary>
    public class AzureSpeechPlayer : ISpeechPlayer, IDisposable
    {
        private readonly ILogger<AzureSpeechPlayer> _logger;
        private readonly AzureConfig _config;
        private SpeechSynthesizer? _synthesizer;
        private AudioConfig? _audioConfig;
        private SpeechConfig? _speechConfig;
        private bool _isPlaying;

        /// <summary>
        /// 播放完成事件
        /// </summary>
        public event Action? OnPlaybackCompleted;

        /// <summary>
        /// 初始化Azure语音播放器
        /// </summary>
        /// <param name="logger">日志记录器</param>
        /// <param name="config">Azure配置</param>
        public AzureSpeechPlayer(
            ILogger<AzureSpeechPlayer> logger,
            IOptions<AzureConfig> config)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = config?.Value ?? throw new ArgumentNullException(nameof(config));

            try
            {
                // 初始化语音配置
                _speechConfig = SpeechConfig.FromSubscription(_config.SpeechKey, _config.SpeechRegion);
                _speechConfig.SpeechSynthesisLanguage = _config.SynthesisLanguage;
                _speechConfig.SpeechSynthesisVoiceName = _config.VoiceName;

                // 创建音频输出配置
                _audioConfig = AudioConfig.FromDefaultSpeakerOutput();

                // 创建语音合成器
                _synthesizer = new SpeechSynthesizer(_speechConfig, _audioConfig);
                _synthesizer.SynthesisCompleted += SynthesizerOnSynthesisCompleted;
                _synthesizer.SynthesisCanceled += SynthesizerOnSynthesisCanceled;

                _logger.LogDebug("Azure语音播放器已初始化");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"初始化Azure语音播放器时发生错误: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 文本转语音并播放
        /// </summary>
        /// <param name="text">要转换的文本</param>
        /// <returns>异步任务</returns>
        public async Task SpeakTextAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || _synthesizer == null)
            {
                _logger.LogWarning("尝试播放空文本或合成器未初始化");
                return;
            }

            try
            {
                _logger.LogDebug($"开始播放文本: \"{text}\"");
                _isPlaying = true;

                // 合成语音
                var result = await _synthesizer.SpeakTextAsync(text);

                // 检查结果
                if (result.Reason == ResultReason.Canceled)
                {
                    var cancellation = SpeechSynthesisCancellationDetails.FromResult(result);
                    _logger.LogError($"语音合成被取消: {cancellation.Reason}, {cancellation.ErrorDetails}");
                    _isPlaying = false;
                    OnPlaybackCompleted?.Invoke();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"文本转语音时发生错误: {ex.Message}");
                _isPlaying = false;
                OnPlaybackCompleted?.Invoke();
            }
        }

        /// <summary>
        /// 播放音频文件
        /// </summary>
        /// <param name="filePath">音频文件路径</param>
        /// <returns>异步任务</returns>
        public async Task PlayAudioFileAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath) || _speechConfig == null)
            {
                _logger.LogWarning($"音频文件不存在或语音配置未初始化: {filePath}");
                return;
            }

            try
            {
                _logger.LogDebug($"开始播放音频文件: {filePath}");
                _isPlaying = true;

                // 使用AudioConfig从文件创建音频输入
                using (var audioConfig = AudioConfig.FromWavFileInput(filePath))
                {
                    // 创建临时合成器用于播放文件
                    using (var player = new SpeechSynthesizer(_speechConfig, audioConfig))
                    {
                        // 直接播放一个空的SSML，这样可以触发音频文件的播放
                        var ssml = $@"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='{_config.SynthesisLanguage}'><voice name='{_config.VoiceName}'></voice></speak>";
                        var result = await player.SpeakSsmlAsync(ssml);

                        // 检查结果
                        if (result.Reason != ResultReason.SynthesizingAudioCompleted)
                        {
                            _logger.LogError($"播放音频文件失败: {result.Reason}");
                        }
                    }
                }

                _isPlaying = false;
                OnPlaybackCompleted?.Invoke();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"播放音频文件时发生错误: {ex.Message}");
                _isPlaying = false;
                OnPlaybackCompleted?.Invoke();
            }
        }

        /// <summary>
        /// 停止当前播放
        /// </summary>
        public void StopPlayback()
        {
            if (!_isPlaying || _synthesizer == null)
            {
                return;
            }

            try
            {
                _logger.LogDebug("正在停止语音播放");
                _synthesizer.StopSpeakingAsync();
                _isPlaying = false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"停止播放时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 语音合成完成事件处理
        /// </summary>
        private void SynthesizerOnSynthesisCompleted(object? sender, SpeechSynthesisEventArgs e)
        {
            _logger.LogDebug("语音合成完成");
            _isPlaying = false;
            OnPlaybackCompleted?.Invoke();
        }

        /// <summary>
        /// 语音合成取消事件处理
        /// </summary>
        private void SynthesizerOnSynthesisCanceled(object? sender, SpeechSynthesisEventArgs e)
        {
            _logger.LogWarning("语音合成已取消");
            _isPlaying = false;
            OnPlaybackCompleted?.Invoke();
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _logger.LogDebug("正在释放AzureSpeechPlayer资源");

            StopPlayback();

            if (_synthesizer != null)
            {
                _synthesizer.SynthesisCompleted -= SynthesizerOnSynthesisCompleted;
                _synthesizer.SynthesisCanceled -= SynthesizerOnSynthesisCanceled;
                _synthesizer.Dispose();
                _synthesizer = null;
            }

            _audioConfig?.Dispose();
            _audioConfig = null;

            // SpeechConfig 无需显式释放

            _logger.LogDebug("AzureSpeechPlayer资源已释放");
        }
    }
} 
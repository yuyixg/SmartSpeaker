using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using SmartSpeaker.Core.Config;
using SmartSpeaker.Core.Interfaces;

namespace SmartSpeaker.Core
{
    /// <summary>
    /// 智能音箱状态枚举
    /// </summary>
    public enum SmartSpeakerState
    {
        /// <summary>
        /// 空闲状态 - 等待唤醒词
        /// </summary>
        Idle,
        
        /// <summary>
        /// 对话状态 - 等待用户输入
        /// </summary>
        Dialogue,
        
        /// <summary>
        /// 处理用户输入状态
        /// </summary>
        Processing,
        
        /// <summary>
        /// 回复状态 - 音箱输出回复
        /// </summary>
        Responding
    }
    
    /// <summary>
    /// 智能音箱控制器
    /// </summary>
    public class SmartSpeakerController : IDisposable
    {
        private readonly ILogger<SmartSpeakerController> _logger;
        
        private readonly IWakeWordDetector _wakeWordDetector;
        private readonly ISpeechRecorder _speechRecorder;
        private readonly ILanguageModel _languageModel;
        private readonly ISpeechPlayer _speechPlayer;
        private readonly SmartSpeakerConfig _speakerConfig;
        private bool _isRunning;
        private bool _isProcessingCommand;
        
        // 状态管理
        private SmartSpeakerState _currentState = SmartSpeakerState.Idle;
        private DateTime _lastInteractionTime;
        private CancellationTokenSource _timeoutCts;
        private List<KeyValuePair<string, string>> _conversationHistory;
        
        // 活动状态跟踪
        private bool _isPlaying;
        private bool _isRecording;
        private bool _isProcessingAIRequest;
        
        // 状态变更事件
        public event Action<SmartSpeakerState> OnStateChanged;

        /// <summary>
        /// 初始化智能音箱控制器
        /// </summary>
        /// <param name="logger">日志记录器</param>
        /// <param name="wakeWordDetector">唤醒词检测器</param>
        /// <param name="speechRecorder">语音录制器</param>
        /// <param name="languageModel">语言模型</param>
        /// <param name="speechPlayer">语音播放器</param>
        /// <param name="speakerOptions">智能音箱配置选项</param>
        public SmartSpeakerController(
            ILogger<SmartSpeakerController> logger,
            IWakeWordDetector wakeWordDetector,
            ISpeechRecorder speechRecorder,
            ILanguageModel languageModel,
            ISpeechPlayer speechPlayer,
            IOptions<SmartSpeakerConfig> speakerOptions)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _wakeWordDetector = wakeWordDetector ?? throw new ArgumentNullException(nameof(wakeWordDetector));
            _speechRecorder = speechRecorder ?? throw new ArgumentNullException(nameof(speechRecorder));
            _languageModel = languageModel ?? throw new ArgumentNullException(nameof(languageModel));
            _speechPlayer = speechPlayer ?? throw new ArgumentNullException(nameof(speechPlayer));
            _speakerConfig = speakerOptions?.Value ?? throw new ArgumentNullException(nameof(speakerOptions));
            
            _logger.LogDebug("SmartSpeakerController 实例已创建");

            // 初始化对话历史记录
            _conversationHistory = new List<KeyValuePair<string, string>>();
            
            // 设置事件处理器
            _wakeWordDetector.OnWakeWordDetected += HandleWakeWordDetected;
            _speechRecorder.OnSpeechRecognized += HandleSpeechRecognized;
            _speechRecorder.OnRecordingStarted += HandleRecordingStarted;
            _speechRecorder.OnRecordingStopped += HandleRecordingStopped;
            _speechPlayer.OnPlaybackCompleted += HandlePlaybackCompleted;
        }
        
        /// <summary>
        /// 获取当前智能音箱的状态
        /// </summary>
        public SmartSpeakerState CurrentState => _currentState;
        
        /// <summary>
        /// 启动智能音箱
        /// </summary>
        public void Start()
        {
            if (_isRunning)
            {
                _logger.LogWarning("智能音箱已经处于运行状态，无需重复启动");
                return;
            }

            _isRunning = true;
            _logger.LogInformation("智能音箱启动中...");
            
            _wakeWordDetector.Start();
            
            TransitionToState(SmartSpeakerState.Idle);
            _logger.LogInformation("智能音箱已启动，等待唤醒词...");
        }
        
        /// <summary>
        /// 停止智能音箱
        /// </summary>
        public void Stop()
        {
            if (!_isRunning)
            {
                _logger.LogWarning("智能音箱已经处于停止状态，无需重复停止");
                return;
            }

            _isRunning = false;
            _logger.LogInformation("智能音箱停止中...");
            
            CancelTimeout();
            
            _wakeWordDetector.Stop();
            
            if (_isRecording)
            {
                _speechRecorder.StopRecording();
            }
            
            if (_isPlaying)
            {
                _speechPlayer.StopPlayback();
            }
            
            _logger.LogInformation("智能音箱已停止");
        }
        
        /// <summary>
        /// 状态转换处理
        /// </summary>
        /// <param name="newState">新的状态</param>
        private void TransitionToState(SmartSpeakerState newState)
        {
            if (_currentState == newState)
            {
                return;
            }
            
            var oldState = _currentState;
            _currentState = newState;
            
            _logger.LogDebug($"状态转换: {oldState} -> {newState}");
            
            // 记录交互时间
            _lastInteractionTime = DateTime.Now;
            
            // 根据新状态执行相应的操作
            switch (newState)
            {
                case SmartSpeakerState.Idle:
                    CancelTimeout();
                    _wakeWordDetector.Resume();
                    break;
                    
                case SmartSpeakerState.Dialogue:
                  //  _wakeWordDetector.Pause();
                    
                    // 开始录音
                    if (!_isRecording)
                    {
                        _speechRecorder.StartRecording();
                    }
                    break;
                    
                case SmartSpeakerState.Processing:
                    if (_isRecording)
                    {
                        _speechRecorder.StopRecording();
                    }
                    CancelTimeout();
                    break;
                    
                case SmartSpeakerState.Responding:
                    CancelTimeout();
                    break;
            }
            
            // 触发状态变更事件
            OnStateChanged?.Invoke(newState);
        }
        
        /// <summary>
        /// 处理唤醒词检测事件
        /// </summary>
        /// <param name="wakeWord">检测到的唤醒词</param>
        private async void HandleWakeWordDetected(string wakeWord)
        {
            if (!_isRunning || _currentState != SmartSpeakerState.Idle)
            {
                return;
            }
            
            _logger.LogInformation($"检测到唤醒词: {wakeWord}");
            
            // 播放唤醒提示音
            if (!string.IsNullOrEmpty(_speakerConfig.WakeSound))
            {
                try
                {
                    await _speechPlayer.PlayAudioFileAsync(_speakerConfig.WakeSound);
                    // 播放完提示音后开始计时
                    StartTimeout();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"播放唤醒提示音失败: {ex.Message}");
                    // 即使播放失败也要开始计时
                    StartTimeout();
                }
            }
            else
            {
                // 没有提示音直接开始计时
                StartTimeout();
            }
            
            // 转换到对话状态
            TransitionToState(SmartSpeakerState.Dialogue);
        }
        
        /// <summary>
        /// 处理语音识别事件
        /// </summary>
        /// <param name="speechText">识别到的语音文本</param>
        private async void HandleSpeechRecognized(string speechText)
        {
            if (!_isRunning || (_currentState != SmartSpeakerState.Dialogue && CurrentState!=SmartSpeakerState.Processing))
            {
                return;
            }
            
            if (string.IsNullOrWhiteSpace(speechText))
            {
                _logger.LogInformation("语音识别结果为空，退出对话");
                await _speechPlayer.SpeakTextAsync("抱歉，我没听到您的问题，再见。");
                TransitionToState(SmartSpeakerState.Idle);
                return;
            }
            
            if (_isProcessingCommand)
            {
                _logger.LogWarning("正在处理上一条命令，忽略新的语音输入");
                return;
            }
            
            _isProcessingCommand = true;
            
            try
            {
                _logger.LogInformation($"识别到用户语音: \"{speechText}\"");
                
                // 转换到处理状态
                TransitionToState(SmartSpeakerState.Processing);
                
                // 防止同时进行多个AI请求
                if (_isProcessingAIRequest)
                {
                    _logger.LogWarning("正在处理上一个AI请求，取消此次请求");
                    TransitionToState(SmartSpeakerState.Idle);
                    return;
                }
                
                _isProcessingAIRequest = true;
                
                try
                {
                    // 将用户输入添加到对话历史
                    _conversationHistory.Add(new KeyValuePair<string, string>("user", speechText));
                    
                    // 保持对话历史在配置的最大长度内
                    while (_conversationHistory.Count > _speakerConfig.MaxConversationHistory * 2)
                    {
                        _conversationHistory.RemoveAt(0);
                        _conversationHistory.RemoveAt(0);
                    }
                    
                    // 请求AI响应
                    var response = await _languageModel.GetResponseAsync(speechText, _conversationHistory);
                    
                    if (string.IsNullOrWhiteSpace(response))
                    {
                        _logger.LogWarning("AI返回了空响应");
                        await _speechPlayer.SpeakTextAsync("抱歉，我没能理解您的问题。");
                        // 转换回空闲状态
                        TransitionToState(SmartSpeakerState.Idle);
                    }
                    else
                    {
                        // 将AI响应添加到对话历史
                        _conversationHistory.Add(new KeyValuePair<string, string>("assistant", response));
                        
                        _logger.LogInformation($"AI响应: \"{response}\"");
                        
                        // 转换到回复状态
                        TransitionToState(SmartSpeakerState.Responding);
                        
                        // 播放AI响应
                        await _speechPlayer.SpeakTextAsync(response);
                    }
                }
                finally
                {
                    _isProcessingAIRequest = false;
                }
                
                // 转换回空闲状态
                TransitionToState(SmartSpeakerState.Dialogue);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"处理语音命令时发生错误: {ex.Message}");
                
                // 播放错误提示
                await _speechPlayer.SpeakTextAsync("抱歉，处理您的请求时出现了错误。");
                
                // 转换回空闲状态
                TransitionToState(SmartSpeakerState.Idle);
            }
            finally
            {
                _isProcessingCommand = false;
            }
        }
        
        /// <summary>
        /// 启动超时计时器
        /// </summary>
        private void StartTimeout()
        {
            return;
            CancelTimeout();
            
            _timeoutCts = new CancellationTokenSource();
            var token = _timeoutCts.Token;
            
            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(_speakerConfig.ListeningTimeout, token);
                    
                    if (token.IsCancellationRequested)
                    {
                        return;
                    }
                    
                    _logger.LogInformation("对话超时，回到空闲状态");
                    
                    // 主线程调用EndDialogue
                    await Task.Run(() => EndDialogue());
                }
                catch (TaskCanceledException)
                {
                    // 超时被取消，不做处理
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"超时处理中发生错误: {ex.Message}");
                }
            }, token);
        }
        
        /// <summary>
        /// 取消超时计时器
        /// </summary>
        private void CancelTimeout()
        {
            if (_timeoutCts != null)
            {
                try
                {
                    _timeoutCts.Cancel();
                    _timeoutCts.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"取消超时计时器时发生错误: {ex.Message}");
                }
                finally
                {
                    _timeoutCts = null;
                }
            }
        }
        
        /// <summary>
        /// 结束对话并返回空闲状态
        /// </summary>
        private void EndDialogue()
        {
            if (_currentState == SmartSpeakerState.Dialogue)
            {
                if (_isRecording)
                {
                    _speechRecorder.StopRecording();
                }
                
                TransitionToState(SmartSpeakerState.Idle);
            }
        }
        
        /// <summary>
        /// 处理播放完成事件
        /// </summary>
        private void HandlePlaybackCompleted()
        {
            _isPlaying = false;
            
            if (_currentState == SmartSpeakerState.Responding)
            {
                // 播放完AI回复后，重新开始计时等待用户输入
                StartTimeout();
                TransitionToState(SmartSpeakerState.Dialogue);
            }
        }
        
        /// <summary>
        /// 处理录音开始事件
        /// </summary>
        private void HandleRecordingStarted()
        {
            _isRecording = true;
            _logger.LogDebug("录音已开始");
        }
        
        /// <summary>
        /// 处理录音结束事件
        /// </summary>
        private void HandleRecordingStopped()
        {
            _isRecording = false;
            _logger.LogDebug("录音已结束");
        }
        
        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _logger.LogDebug("正在释放SmartSpeakerController资源");
            
            Stop();
            
            _wakeWordDetector.OnWakeWordDetected -= HandleWakeWordDetected;
            _speechRecorder.OnSpeechRecognized -= HandleSpeechRecognized;
            _speechRecorder.OnRecordingStarted -= HandleRecordingStarted;
            _speechRecorder.OnRecordingStopped -= HandleRecordingStopped;
            _speechPlayer.OnPlaybackCompleted -= HandlePlaybackCompleted;
            
            CancelTimeout();
            
            // 如果依赖项实现了IDisposable，应该在这里释放它们
            
            _logger.LogDebug("SmartSpeakerController资源已释放");
        }
    }
}
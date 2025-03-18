using System;

namespace SmartSpeaker.Core.Interfaces
{
    /// <summary>
    /// 语音录制器接口
    /// </summary>
    public interface ISpeechRecorder
    {
        /// <summary>
        /// 语音识别事件
        /// </summary>
        event Action<string> OnSpeechRecognized;
        
        /// <summary>
        /// 录音开始事件
        /// </summary>
        event Action OnRecordingStarted;
        
        /// <summary>
        /// 录音停止事件
        /// </summary>
        event Action OnRecordingStopped;

        /// <summary>
        /// 是否正在录音
        /// </summary>
        bool IsRecording { get; }
        
        /// <summary>
        /// 开始录音
        /// </summary>
        void StartRecording();
        
        /// <summary>
        /// 停止录音
        /// </summary>
        void StopRecording();
    }
} 
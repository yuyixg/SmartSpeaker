namespace SmartSpeaker.Core.Interfaces
{
    /// <summary>
    /// 唤醒词检测器接口
    /// </summary>
    public interface IWakeWordDetector
    {
        /// <summary>
        /// 唤醒词检测到事件
        /// </summary>
        event Action<string> OnWakeWordDetected;
        
        /// <summary>
        /// 启动唤醒词检测
        /// </summary>
        void Start();
        
        /// <summary>
        /// 停止唤醒词检测
        /// </summary>
        void Stop();
        
        /// <summary>
        /// 暂停唤醒词检测
        /// </summary>
        void Pause();
        
        /// <summary>
        /// 恢复唤醒词检测
        /// </summary>
        void Resume();
    }
} 
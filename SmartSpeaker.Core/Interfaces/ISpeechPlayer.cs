using System.Threading.Tasks;

namespace SmartSpeaker.Core.Interfaces
{
    /// <summary>
    /// 语音播放器接口
    /// </summary>
    public interface ISpeechPlayer
    {
        /// <summary>
        /// 播放完成事件
        /// </summary>
        event Action OnPlaybackCompleted;
        
        /// <summary>
        /// 文本转语音并播放
        /// </summary>
        /// <param name="text">要转换的文本</param>
        /// <returns>异步任务</returns>
        Task SpeakTextAsync(string text);
        
        /// <summary>
        /// 播放音频文件
        /// </summary>
        /// <param name="filePath">音频文件路径</param>
        /// <returns>异步任务</returns>
        Task PlayAudioFileAsync(string filePath);
        
        /// <summary>
        /// 停止当前播放
        /// </summary>
        void StopPlayback();
    }
} 
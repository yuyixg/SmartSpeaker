using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartSpeaker.Core.Interfaces
{
    /// <summary>
    /// 语言模型接口
    /// </summary>
    public interface ILanguageModel
    {
        /// <summary>
        /// 获取AI响应
        /// </summary>
        /// <param name="prompt">用户输入</param>
        /// <param name="conversationHistory">对话历史记录</param>
        /// <returns>AI响应文本</returns>
        Task<string> GetResponseAsync(string prompt, List<KeyValuePair<string, string>> conversationHistory);
    }
} 
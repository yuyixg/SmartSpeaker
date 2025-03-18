using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using SmartSpeaker.Core.Config;
using SmartSpeaker.Core.Interfaces;

namespace SmartSpeaker.Core.Services
{
    /// <summary>
    /// OpenAI 语言模型服务
    /// </summary>
    public class OpenAILanguageModel : ILanguageModel
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<OpenAILanguageModel> _logger;
        private readonly OpenAIConfig _config;

        /// <summary>
        /// 初始化 OpenAI 语言模型服务
        /// </summary>
        /// <param name="httpClient">HTTP客户端</param>
        /// <param name="logger">日志记录器</param>
        /// <param name="config">OpenAI配置</param>
        public OpenAILanguageModel(
            HttpClient httpClient,
            ILogger<OpenAILanguageModel> logger,
            IOptions<OpenAIConfig> config)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = config?.Value ?? throw new ArgumentNullException(nameof(config));

            // 配置HttpClient
            _httpClient.BaseAddress = new Uri(_config.BaseUrl);
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_config.ApiKey}");
        }

        /// <summary>
        /// 获取AI响应
        /// </summary>
        /// <param name="prompt">用户输入</param>
        /// <param name="conversationHistory">对话历史记录</param>
        /// <returns>AI响应文本</returns>
        public async Task<string> GetResponseAsync(string prompt, List<KeyValuePair<string, string>> conversationHistory)
        {
            try
            {
                _logger.LogDebug($"正在向OpenAI发送请求: {prompt}");

                // 构建消息列表
                var messages = new List<object>
                {
                    new { role = "system", content = _config.SystemPrompt }
                };

                // 添加对话历史
                foreach (var entry in conversationHistory)
                {
                    messages.Add(new { role = entry.Key, content = entry.Value });
                }

                // 构建请求数据
                var requestData = new
                {
                    model = _config.Model,
                    messages,
                    max_tokens = _config.MaxTokens,
                    temperature = _config.Temperature
                };

                // 序列化请求数据
                var requestContent = new StringContent(
                    JsonSerializer.Serialize(requestData),
                    Encoding.UTF8,
                    "application/json");

                // 发送请求
                var response = await _httpClient.PostAsync("/v1/chat/completions", requestContent);

                // 检查响应状态码
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"OpenAI API返回错误: {response.StatusCode}, {errorContent}");
                    return "抱歉，我暂时无法回答您的问题。";
                }

                // 解析响应
                var responseContent = await response.Content.ReadAsStringAsync();
                var responseData = JsonSerializer.Deserialize<JsonElement>(responseContent);

                // 提取回复文本
                string replyText = responseData
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString() ?? string.Empty;

                _logger.LogDebug($"OpenAI响应: {replyText}");

                return replyText;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"调用OpenAI API时发生错误: {ex.Message}");
                return "抱歉，我暂时无法回答您的问题。";
            }
        }
    }
} 
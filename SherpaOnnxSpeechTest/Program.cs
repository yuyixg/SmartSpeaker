using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmartSpeaker.Core.Config;
using SmartSpeaker.Core.Interfaces;
using SmartSpeaker.Core.Services;

class Program
{
    static async Task Main(string[] args)
    {
        try
        {
            // 创建配置
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddUserSecrets<Program>()
                .Build();

            // 创建服务集合
            var services = new ServiceCollection();

            // 添加日志
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Debug);
            });

            // 添加配置
            services.Configure<SherpaOnnxSpeechConfig>(
                configuration.GetSection("SherpaOnnxSpeech"));

            // 添加语音录制器
            services.AddSingleton<ISpeechRecorder, SherpaOnnxSpeechRecorder>();

            // 构建服务提供者
            var serviceProvider = services.BuildServiceProvider();

            // 获取语音录制器
            var speechRecorder = serviceProvider.GetRequiredService<ISpeechRecorder>();

            // 订阅事件
            speechRecorder.OnSpeechRecognized += (text) =>
            {
                Console.WriteLine($"识别到语音: {text}");
            };

            speechRecorder.OnRecordingStarted += () =>
            {
                Console.WriteLine("开始录音");
            };

            speechRecorder.OnRecordingStopped += () =>
            {
                Console.WriteLine("停止录音");
            };

            Console.WriteLine("按回车键开始录音，再次按回车键停止录音...");
            Console.WriteLine("按 'q' 键退出程序");

            while (true)
            {
                var key = Console.ReadKey(true);
                
                if (key.Key == ConsoleKey.Q)
                {
                    break;
                }
                
                if (key.Key == ConsoleKey.Enter)
                {
                    if (!speechRecorder.IsRecording)
                    {
                        speechRecorder.StartRecording();
                    }
                    else
                    {
                        speechRecorder.StopRecording();
                    }
                }
            }

            // 清理资源
            if (speechRecorder is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"发生错误: {ex.Message}");
            Console.WriteLine($"堆栈跟踪: {ex.StackTrace}");
        }
    }
} 
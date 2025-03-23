using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using SmartSpeaker.Core;
using SmartSpeaker.Core.Config;
using SmartSpeaker.Core.Interfaces;
using SmartSpeaker.Core.Services;

namespace SmartSpeaker.Console
{
    class Program
    {
        static async Task Main(string[] args)
        {
            System.Console.OutputEncoding = System.Text.Encoding.UTF8;
            System.Console.InputEncoding = System.Text.Encoding.UTF8;
            
            // 配置Serilog
            var loggerConfiguration = new LoggerConfiguration()
                .MinimumLevel.Information()  // 基本最小级别设置为Information
                // 过滤所有Microsoft命名空间下的低级别日志
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.Extensions", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                // 过滤System命名空间下的低级别日志
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .MinimumLevel.Override("System.Net.Http", LogEventLevel.Warning)
                .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Debug) // 控制台输出Info级别及以上日志
                .WriteTo.File(
                    path: Path.Combine("logs", "smartspeaker_.log"),
                    restrictedToMinimumLevel: LogEventLevel.Information, // 文件输出Info级别及以上日志
                    rollingInterval: RollingInterval.Day,  // 每天一个日志文件
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
                );
            
            // 创建Serilog记录器
            Log.Logger = loggerConfiguration.CreateLogger();
            
            // 记录应用程序启动信息
            Log.Information("=== 小智智能音箱 ===");
            Log.Information("正在初始化...");

            try
            {
                // 创建配置
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .AddUserSecrets<Program>()
                    .AddEnvironmentVariables()
                    .Build();

                // 初始化模型目录
                InitializeModelDirectories(configuration);

                // 创建服务容器
                var services = new ServiceCollection();

                // 注册配置
                services.AddOptions();
                services.Configure<AzureConfig>(options => 
                    configuration.GetSection("Azure").Bind(options));
                services.Configure<OpenAIConfig>(options => 
                    configuration.GetSection("OpenAI").Bind(options));
                services.Configure<SmartSpeakerConfig>(options => 
                    configuration.GetSection("SmartSpeaker").Bind(options));
                services.Configure<SherpaOnnxConfig>(options =>
                    configuration.GetSection("SherpaOnnx").Bind(options));
                services.Configure<SherpaOnnxSpeechConfig>(options =>
                    configuration.GetSection("SherpaOnnxSpeech").Bind(options));

                // 注册日志
                services.AddLogging(builder => 
                {
                    builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Debug);
                    builder.AddSerilog(dispose: true); // 使用Serilog作为日志提供程序
                });

                // 注册HttpClient
                services.AddHttpClient<ILanguageModel, OpenAILanguageModel>();

                // 注册服务
                // 根据配置选择要使用的唤醒词检测实现
                var useSherpaOnnx = configuration.GetValue<bool>("UseSherpaOnnx");
                if (useSherpaOnnx)
                {
                    // 使用sherpa-onnx唤醒词检测器
                    services.AddSingleton<IWakeWordDetector, SherpaOnnxWakeWordDetector>();
                    Log.Information("使用sherpa-onnx进行唤醒词检测");
                    
                    // 使用sherpa-onnx进行语音识别
                    services.AddSingleton<ISpeechRecorder, SherpaOnnxSpeechRecorder>();
                    Log.Information("使用sherpa-onnx进行语音识别");
                }
                else
                {
                    // 使用Azure Speech唤醒词检测器
                    services.AddSingleton<IWakeWordDetector, AzureWakeWordDetector>();
                    Log.Information("使用Azure Speech进行唤醒词检测");
                    
                    // 使用Azure Speech进行语音识别
                    services.AddSingleton<ISpeechRecorder, AzureSpeechRecorder>();
                    Log.Information("使用Azure Speech进行语音识别");
                }
                
                services.AddSingleton<ISpeechPlayer, AzureSpeechPlayer>();
                services.AddSingleton<SmartSpeakerController>();

                // 构建服务提供者
                var serviceProvider = services.BuildServiceProvider();

                // 获取智能音箱控制器
                var smartSpeaker = serviceProvider.GetRequiredService<SmartSpeakerController>();
                
                // 启动智能音箱
                var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
                logger.LogDebug("正在启动智能音箱控制器...");
                smartSpeaker.Start();

                logger.LogInformation("智能音箱已启动...");
                System.Console.WriteLine("按 'q' 键退出");

                // 等待用户按键退出
                while (true)
                {
                    var key = System.Console.ReadKey(true);
                    if (key.KeyChar == 'q' || key.KeyChar == 'Q')
                    {
                        break;
                    }
                }

                // 停止智能音箱
                logger.LogInformation("正在停止智能音箱...");
                smartSpeaker.Stop();
                logger.LogInformation("智能音箱已停止");

                // 释放服务提供者
                if (serviceProvider is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"发生错误: {ex.Message}");
                System.Console.WriteLine($"发生错误: {ex.Message}");
                System.Console.WriteLine(ex.StackTrace);
            }
            finally
            {
                Log.Information("智能音箱已关闭");
                Log.CloseAndFlush(); // 确保日志被完全写入
            }
        }

        /// <summary>
        /// 初始化模型目录
        /// </summary>
        /// <param name="configuration">配置</param>
        private static void InitializeModelDirectories(IConfiguration configuration)
        {
            try
            {
                // 获取配置的模型目录
                var speechConfig = new SherpaOnnxSpeechConfig();
                var kwsConfig = new SherpaOnnxConfig();
                
                configuration.GetSection("SherpaOnnxSpeech").Bind(speechConfig);
                configuration.GetSection("SherpaOnnx").Bind(kwsConfig);
                
                var speechModelDir = Path.GetFullPath(speechConfig.ModelDir);
                var kwsModelDir = Path.GetFullPath(kwsConfig.ModelDir);
                
                Log.Information($"语音识别模型目录: {speechModelDir}");
                Log.Information($"唤醒词检测模型目录: {kwsModelDir}");
                
                // 创建模型目录
                CreateModelDirectory(speechModelDir, "语音识别");
                CreateModelDirectory(kwsModelDir, "唤醒词检测");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "初始化模型目录时发生错误");
            }
        }
        
        /// <summary>
        /// 创建模型目录并添加README文件
        /// </summary>
        /// <param name="directory">目录路径</param>
        /// <param name="modelType">模型类型描述</param>
        private static void CreateModelDirectory(string directory, string modelType)
        {
            try
            {
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                    Log.Information($"已创建{modelType}模型目录: {directory}");
                    
                    // 创建README文件
                    string readmePath = Path.Combine(directory, "README.txt");
                    string readmeContent = $@"{modelType}模型目录

此目录用于存放{modelType}所需的模型文件。请将以下文件放置在此目录中：

1. encoder.onnx - 编码器模型文件
2. decoder.onnx - 解码器模型文件
3. joiner.onnx - 连接器模型文件
4. tokens.txt - 词表文件

您可以从以下地址下载sherpa-onnx模型：
https://github.com/k2-fsa/sherpa-onnx/releases

如有问题，请参考项目文档。
";
                    File.WriteAllText(readmePath, readmeContent);
                    Log.Information($"已在{modelType}模型目录中创建README文件");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"创建{modelType}模型目录时发生错误: {directory}");
            }
        }
    }
} 
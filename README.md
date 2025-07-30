#  C# 智能语音项目

## 系统要求

- Windows 10 或更高版本
- .NET 6.0 SDK 或更高版本
- 麦克风设备
- 足够的磁盘空间（用于存储模型文件）

## 快速开始

1. 克隆或下载项目到本地
2. 运行设置脚本：
   ```
   setup_and_run.bat
   ```
   这个脚本会：
   - 检查 .NET SDK 是否安装
   - 还原 NuGet 包
   - 构建解决方案
   - 设置模型文件

3. 运行测试程序：
   ```
   cd SherpaOnnxSpeechTest
   dotnet run
   ```

## 项目结构

```
.
├── SmartSpeaker/              # 主项目
│   ├── Models/               # 模型文件目录
│   └── Services/             # 服务实现
├── SherpaOnnxSpeechTest/     # 测试项目
│   ├── models/              # 测试用模型文件
│   ├── Program.cs           # 测试程序入口
│   └── setup_models.bat     # 模型文件设置脚本
├── SmartSpeaker.sln         # 解决方案文件
└── setup_and_run.bat        # 项目设置脚本
```

## 模型文件

测试程序需要以下模型文件：
- encoder.onnx
- decoder.onnx
- joiner.onnx
- tokens.txt

这些文件可以通过以下方式获取：

1. 从主项目复制（推荐）：
   - 运行 `setup_models.bat` 脚本自动复制

2. 手动下载：
   - 访问 https://github.com/k2-fsa/sherpa-onnx/releases
   - 下载中文语音识别模型包
   - 解压并将文件复制到 `SherpaOnnxSpeechTest/models` 目录

## 配置说明

配置文件位于 `SherpaOnnxSpeechTest/appsettings.json`，包含以下主要设置：

```json
{
  "SherpaOnnxSpeech": {
    "ModelDir": "models",
    "EncoderModel": "encoder.onnx",
    "DecoderModel": "decoder.onnx",
    "JoinerModel": "joiner.onnx",
    "TokensFile": "tokens.txt",
    "Provider": "cpu",
    "NumThreads": 1,
    "SampleRate": 16000
  }
}
```

## 常见问题

1. 程序初始化时卡住
   - 检查模型文件是否存在且完整
   - 确认模型文件版本兼容性
   - 查看程序输出的详细日志

2. 无法识别语音
   - 检查麦克风设备是否正常工作
   - 确认系统音量设置
   - 尝试调整说话距离和音量

3. 模型文件缺失
   - 运行 `setup_models.bat` 脚本
   - 或手动从主项目复制模型文件

## 开发说明

1. 使用 Visual Studio：
   - 打开 `SmartSpeaker.sln`
   - 设置 `SherpaOnnxSpeechTest` 为启动项目

2. 使用 VS Code：
   - 打开项目文件夹
   - 安装 C# 扩展
   - 使用 F5 启动调试

## 注意事项

- 确保有足够的磁盘空间存储模型文件
- 首次运行可能需要一些时间来加载模型
- 建议使用 CPU 版本的模型进行测试
- 如果使用 GPU 加速，请确保安装了相应的 CUDA 驱动

## 技术支持

如果遇到问题，请：
1. 检查错误日志
2. 确认模型文件完整性
3. 验证系统要求是否满足
4. 查看常见问题解决方案 

SherpaOnnx 模型文件说明
===================

为了使测试程序正常工作，请将以下 SherpaOnnx 语音识别模型文件放在此目录中：

1. encoder.onnx - 编码器模型文件
2. decoder.onnx - 解码器模型文件
3. joiner.onnx - 连接器模型文件
4. tokens.txt - 词表文件

## 获取模型文件

您可以通过以下方式获取模型文件：

1. 从 SmartSpeaker/Models/sherpa-onnx-speech 目录复制
   - 如果您已有智能音箱项目，可以直接从其模型目录复制

2. 从 SherpaOnnx 官方资源下载
   - 访问 https://github.com/k2-fsa/sherpa-onnx/releases
   - 下载中文语音识别模型包，例如 "sherpa-onnx-streaming-zipformer-zh-14M-2023-02-23.tar.bz2"

3. 使用项目中已配置的模型
   - 如果您修改了 appsettings.json 文件中的模型路径，请确保那个路径下有对应的模型文件

## 重要提示

1. 确保模型文件名与配置文件中指定的名称一致：
   - 查看 appsettings.json 文件中的 SherpaOnnxSpeech 部分
   - 默认名称为 encoder.onnx, decoder.onnx, joiner.onnx 和 tokens.txt
   - 如使用不同的文件名，请在测试程序中使用"自定义配置"选项修改

2. 模型文件通常较大（可能超过100MB），请确保有足够的磁盘空间

3. 如果使用 quantized（量化）模型（如int8版本），可获得更好的性能

4. 检查音频设备
   - 在开始测试前，确保麦克风设备已正确连接且工作正常
   - 测试程序会使用系统默认麦克风设备

## 常见错误解决方案

1. 程序初始化时卡住
   - 通常是模型文件不存在或不兼容导致
   - 确保使用了正确版本的模型文件
   - 检查模型文件是否完整（未损坏）

2. 无法识别语音
   - 检查麦克风是否正常工作
   - 确认音量设置是否正确
   - 尝试靠近麦克风说话

注意：
- 大多数语音识别模型的采样率为16000Hz
- 如果您的电脑支持CUDA或其他硬件加速，可以在测试程序中将Provider设置为相应的值（如"cuda"）
- 对于大多数情况，默认的线程数(1)已足够，但您可以根据CPU核心数适当增加 
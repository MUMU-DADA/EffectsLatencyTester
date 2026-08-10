# 效果器延迟检测（.NET 原型）

[English](../README.md) · [简体中文](README.zh-CN.md) · [繁體中文](README.zh-TW.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Español](README.es.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [Italiano](README.it.md) · [Português (Brasil)](README.pt-BR.md) · [Русский](README.ru.md)

这是一个使用 Avalonia 构建的 .NET 8 跨平台桌面程序，用于测量声卡与效果器板的完整往返延迟。Windows 使用 ASIO，macOS 使用 Core Audio，Linux 通过 PortAudio 使用 ALSA/PipeWire/JACK。

## 运行

```powershell
dotnet restore
dotnet run --project .\EffectsLatencyTester.csproj
```

## 界面截图

![程序运行截图](Snipaste.png)

程序会优先选择第一个可打开的音频设备，读取采样率和 buffer，列出输入/输出通道，发送测试脉冲并检测返回脉冲。测试完成后会显示输入、输出和叠加波形及时间轴。

## 硬件连接

1. 将声卡 音频输出连接到效果器板输入。
2. 将效果器板输出连接回声卡 音频输入。
3. 关闭 Direct Monitoring，避免直通信号干扰检测。
4. 从低音量开始测试。

## 延迟测量

第一次结果是声卡输出、效果器板和声卡输入的总往返延迟。若要得到效果器板延迟，先将声卡输出直接接回输入测量基准，再测量效果器回路：

```text
效果器板延迟 = 效果器回路总延迟 - 声卡直连基准延迟
```

波形区域支持普通滚轮横向移动、拖动横向移动和 `Ctrl` + 滚轮缩放。连续单击两次可记录 T1/T2 并显示时间差。即使没有检测到返回脉冲，波形也会保留以便检查接线、通道和音量。

## 国际化

界面会在启动时读取系统 UI 语言。目前提供简体中文、繁体中文、日语、韩语、西班牙语、法语、德语、意大利语、巴西葡萄牙语和俄语；没有资源的语言回退到英语。也可以通过启动参数指定本次运行的语言：

```powershell
dotnet run --project .\EffectsLatencyTester.csproj -- --lang zh-CN
# 或者已发布程序：
.\EffectsLatencyTester_win-x64.exe --language=ja-JP
```

## 发布

```powershell
.\publish.ps1
```

默认生成以下自包含单文件程序：

```text
artifacts/publish/win-x86/EffectsLatencyTester_win-x86.exe
artifacts/publish/win-x64/EffectsLatencyTester_win-x64.exe
artifacts/publish/win-arm64/EffectsLatencyTester_win-arm64.exe
artifacts/publish/osx-x64/EffectsLatencyTester_osx-x64
artifacts/publish/osx-arm64/EffectsLatencyTester_osx-arm64
artifacts/publish/linux-x64/EffectsLatencyTester_linux-x64
artifacts/publish/linux-arm64/EffectsLatencyTester_linux-arm64
```

也可以使用 `-Runtime win-x64`、`-Runtime osx-arm64` 或 `-Runtime linux-x64` 只发布一个目标。目标电脑无需安装 .NET；Windows 需要匹配架构的 ASIO 驱动，macOS 和 Linux 通过 PortAudio 使用系统音频接口。macOS/Linux 路径还需要在目标系统和真实声卡上进一步验证。

每个发布文件的名称格式为 `EffectsLatencyTester_<os-arch>`；Windows 目标使用 `.exe` 后缀，macOS 和 Linux 不使用扩展名。

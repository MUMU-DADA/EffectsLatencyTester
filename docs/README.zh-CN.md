# 效果器延迟检测（.NET 原型）

[English](../README.md) · [简体中文](README.zh-CN.md) · [繁體中文](README.zh-TW.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Español](README.es.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [Italiano](README.it.md) · [Português (Brasil)](README.pt-BR.md) · [Русский](README.ru.md)

这是一个 Windows WPF/.NET 8 原型，使用 `NAudio.Asio` 列出和打开本机 ASIO 驱动，并测量声卡与效果器板的完整往返延迟。

## 运行

```powershell
dotnet restore
dotnet run --project .\EffectsLatencyTester.csproj
```

程序会优先选择第一个可打开的驱动，读取采样率和 buffer，列出输入/输出通道，发送测试脉冲并检测返回脉冲。测试完成后会显示输入、输出和叠加波形及时间轴。

## 硬件连接

1. 将声卡 ASIO 输出连接到效果器板输入。
2. 将效果器板输出连接回声卡 ASIO 输入。
3. 关闭 Direct Monitoring，避免直通信号干扰检测。
4. 从低音量开始测试。

## 延迟测量

第一次结果是声卡输出、效果器板和声卡输入的总往返延迟。若要得到效果器板延迟，先将声卡输出直接接回输入测量基准，再测量效果器回路：

```text
效果器板延迟 = 效果器回路总延迟 - 声卡直连基准延迟
```

波形区域支持普通滚轮横向移动、拖动横向移动和 `Ctrl` + 滚轮缩放。连续单击两次可记录 T1/T2 并显示时间差。即使没有检测到返回脉冲，波形也会保留以便检查接线、通道和音量。

## 国际化

界面会在启动时读取系统 UI 语言。目前提供简体中文、繁体中文、日语、韩语、西班牙语、法语、德语、意大利语、巴西葡萄牙语和俄语；没有资源的语言回退到英语。

## 发布

```powershell
.\publish.ps1
```

默认生成三个 Windows 自包含单文件程序：

```text
artifacts/publish/win-x86/EffectsLatencyTester.exe
artifacts/publish/win-x64/EffectsLatencyTester.exe
artifacts/publish/win-arm64/EffectsLatencyTester.exe
```

也可以使用 `-Runtime win-x64`、`-Runtime win-x86` 或 `-Runtime win-arm64` 只发布一种架构。目标电脑无需安装 .NET，但必须安装匹配架构的 ASIO 驱动。当前项目使用 WPF 和 `NAudio.Asio`，暂不支持 Linux/macOS。

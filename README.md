# Effects Pedal Latency Tester (.NET prototype)

[English](README.md) · [简体中文](docs/README.zh-CN.md) · [繁體中文](docs/README.zh-TW.md) · [日本語](docs/README.ja.md) · [한국어](docs/README.ko.md) · [Español](docs/README.es.md) · [Français](docs/README.fr.md) · [Deutsch](docs/README.de.md) · [Italiano](docs/README.it.md) · [Português (Brasil)](docs/README.pt-BR.md) · [Русский](docs/README.ru.md)

This Windows WPF/.NET 8 prototype uses `NAudio.Asio` to list and open local ASIO drivers, then measures the complete round-trip latency through an audio interface and an effects pedalboard.

## Run

```powershell
dotnet restore
dotnet run --project .\LatencyTester.csproj
```

The application can list ASIO drivers, prefer the first driver that can be opened, read supported sample rates and buffer sizes, list named input/output channels, generate test pulses, detect the return pulse, measure a direct-interface baseline, measure the effects loop, and display input/output waveforms with a time axis.

## Hardware connection

1. Connect the audio interface ASIO output to the effects board input.
2. Connect the effects board output back to an audio interface ASIO input.
3. Disable Direct Monitoring to avoid a dry signal interfering with detection.
4. Start with a low output volume.

## Measurement

The first result is the total round-trip latency of the audio interface output, effects board, and audio interface input. To isolate the effects-board latency, first connect the interface output directly to its input and measure the baseline, then measure the effects loop:

```text
Effects-board latency = Effects-loop total latency - Direct-interface baseline latency
```

Use the same audio interface for ASIO input and output, keep Direct Monitoring disabled, and use the sample rate, input channel, and output channel selected in the application.

The waveform view shows the latest test as separate input, output, and combined panels. The red curve is the generated output pulse and the cyan curve is the input return. The normal mouse wheel pans horizontally, dragging pans horizontally, and `Ctrl` + mouse wheel zooms. Two clicks record T1 and T2 and show their time difference. A waveform is kept even when no return pulse is detected so that cabling, channel selection, and volume can be checked.

The buffer list is built from the ASIO driver's minimum, maximum, granularity, and preferred values. If the driver rejects the selected buffer, the application reports the error. A driver may also be unavailable when another audio application has opened it.

## Internationalization

`i18n/Strings.resx` is the English default resource. Localized resources are provided for Simplified Chinese, Traditional Chinese, Japanese, Korean, Spanish, French, German, Italian, Brazilian Portuguese, and Russian. The application reads the system UI culture at startup; unsupported cultures fall back to English. Add a file named `i18n/Strings.<culture>.resx` to add another locale.

## Publish

The project includes a pixel-style icon. `Assets/LatencyTesterIcon.svg` is the editable vector reference and `Assets/LatencyTesterIcon.ico` is used by the build. Run the following command to publish all Windows architectures as self-contained single-file applications:

```powershell
.\publish.ps1
```

The output files are:

```text
artifacts/publish/win-x86/LatencyTester.exe
artifacts/publish/win-x64/LatencyTester.exe
artifacts/publish/win-arm64/LatencyTester.exe
```

The target computer does not need the .NET runtime, but it still needs Windows and an ASIO driver that matches the application architecture. The ASIO driver itself is not bundled.

To publish only one architecture:

```powershell
.\publish.ps1 -Runtime win-x64
.\publish.ps1 -Runtime win-x86
.\publish.ps1 -Runtime win-arm64
```

`win-x64` and `win-x86` are verified targets. `win-arm64` can be published but requires an ARM64 ASIO driver. Linux and macOS are not currently supported because the application uses Windows WPF and `NAudio.Asio`; supporting them requires a cross-platform UI and audio backend.

`NAudio.Asio` is a Windows-only ASIO backend. If only a 32-bit ASIO driver is available, publish with `-Runtime win-x86`.

The driver list contains drivers registered in Windows and does not guarantee that the hardware is online. For example, a Katana driver may be registered while the device is disconnected, sleeping, or in use by another audio application; this does not affect other available ASIO devices.

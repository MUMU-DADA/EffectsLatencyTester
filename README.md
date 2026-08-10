# Effects Pedal Latency Tester

[English](README.md) · [简体中文](docs/README.zh-CN.md) · [繁體中文](docs/README.zh-TW.md) · [日本語](docs/README.ja.md) · [한국어](docs/README.ko.md) · [Español](docs/README.es.md) · [Français](docs/README.fr.md) · [Deutsch](docs/README.de.md) · [Italiano](docs/README.it.md) · [Português (Brasil)](docs/README.pt-BR.md) · [Русский](docs/README.ru.md)

EffectsLatencyTester is a .NET 8 desktop application for measuring the round-trip latency of an audio interface and guitar effects chain. The UI is built with Avalonia so the application can target Windows, macOS, and Linux. Audio access is isolated behind a common backend interface: Windows uses ASIO, while macOS and Linux use PortAudio with the platform host APIs (Core Audio, ALSA/PipeWire/JACK).

## Architecture

```text
Avalonia UI
    ↓
Common audio interface and latency measurement core
    ↓
Windows: ASIO       macOS: Core Audio       Linux: ALSA/PipeWire/JACK
```

The shared core contains pulse generation, latency detection, waveform data, and CSV/WAV/ZIP export. Platform-specific code is responsible for device enumeration, stream creation, sample format conversion, and buffer callbacks.

## Run

```powershell
dotnet restore
dotnet run --project .\EffectsLatencyTester.csproj
```

The project keeps the NuGet package cache and .NET CLI state in `.nuget-packages/` and `.dotnet-home/` when using the project scripts. These directories are ignored by Git.

## Screenshot

![Application screenshot](docs/Snipaste.png)

The application can list available devices, prefer the first device that can be opened, read supported sample rates and buffer sizes, select input/output channels, generate test pulses, detect the return pulse, measure a direct-interface baseline, measure the effects loop, and display input/output waveforms with a time axis.

The waveform view shows separate input, output, and combined panels. The normal mouse wheel pans horizontally, dragging pans horizontally, and `Ctrl` + mouse wheel zooms. Two clicks record T1 and T2 and show their time difference. A waveform is kept even when no return pulse is detected so cabling, channel selection, and volume can be checked.

## Hardware connection

1. Connect the audio interface output to the effects board input.
2. Connect the effects board output back to an audio interface input.
3. Disable Direct Monitoring to avoid a dry signal interfering with detection.
4. Start with a low output volume.

The effects-board latency is calculated as:

```text
Effects-board latency = Effects-loop total latency - Direct-interface baseline latency
```

Use the same audio interface for input and output, keep Direct Monitoring disabled, and use the sample rate, input channel, and output channel selected in the application.

## Internationalization

`i18n/Strings.resx` is the English default resource. Localized resources are provided for Simplified Chinese, Traditional Chinese, Japanese, Korean, Spanish, French, German, Italian, Brazilian Portuguese, and Russian. The application reads the system UI culture at startup; you can override the language for one launch with `--lang` or `--language`:

```powershell
dotnet run --project .\EffectsLatencyTester.csproj -- --lang zh-CN
# or, after publishing:
.\EffectsLatencyTester_win-x64.exe --language=ja-JP
```

## Publish

The project includes a pixel-style icon and a cross-platform publishing script. Run the following command to publish all configured targets as self-contained single-file applications:

```powershell
.\publish.ps1
```

The output targets are:

```text
artifacts/publish/win-x86/EffectsLatencyTester_win-x86.exe
artifacts/publish/win-x64/EffectsLatencyTester_win-x64.exe
artifacts/publish/win-arm64/EffectsLatencyTester_win-arm64.exe
artifacts/publish/osx-x64/EffectsLatencyTester_osx-x64
artifacts/publish/osx-arm64/EffectsLatencyTester_osx-arm64
artifacts/publish/linux-x64/EffectsLatencyTester_linux-x64
artifacts/publish/linux-arm64/EffectsLatencyTester_linux-arm64
```

The target computer does not need the .NET runtime. Windows builds require an ASIO driver that matches the application architecture; the ASIO driver itself is not bundled. The macOS and Linux binaries include the PortAudio native runtime. Their device enumeration and duplex callback paths still need validation on the target operating system with real audio hardware before production measurements.

To publish one target:

```powershell
.\publish.ps1 -Runtime win-x64
.\publish.ps1 -Runtime osx-arm64
.\publish.ps1 -Runtime linux-x64
```

Each published file is named `EffectsLatencyTester_<os-arch>`; Windows targets use the `.exe` suffix, while macOS and Linux targets have no extension.

## License

This project is licensed under the [MIT License](LICENSE).

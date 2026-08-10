# 效果器延遲檢測（.NET 原型）

[English](../README.md) · [简体中文](README.zh-CN.md) · [繁體中文](README.zh-TW.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Español](README.es.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [Italiano](README.it.md) · [Português (Brasil)](README.pt-BR.md) · [Русский](README.ru.md)

這是一個使用 Avalonia 建立的 .NET 8 跨平台桌面程式，用於測量聲卡與效果器板的完整往返延遲。Windows 使用 ASIO，macOS 使用 Core Audio，Linux 透過 PortAudio 使用 ALSA/PipeWire/JACK。

## 執行

```powershell
dotnet restore
dotnet run --project .\EffectsLatencyTester.csproj
```

## 介面截圖

![程式執行畫面](Snipaste.png)

程式會優先選擇第一個可開啟的音訊裝置，讀取取樣率與緩衝區，列出輸入/輸出通道，發送測試脈衝並偵測返回脈衝。測試後會顯示輸入、輸出和疊加波形及時間軸。

## 硬體連接

1. 將聲卡 音訊輸出連接到效果器板輸入。
2. 將效果器板輸出接回聲卡 音訊輸入。
3. 關閉 Direct Monitoring，避免直通信號干擾偵測。
4. 從低音量開始測試。

## 延遲測量

第一次結果是聲卡輸出、效果器板和聲卡輸入的總往返延遲。若要取得效果器板延遲，先將聲卡輸出直接接回輸入測量基準，再測量效果器回路：

```text
效果器板延遲 = 效果器回路總延遲 - 聲卡直連基準延遲
```

波形區域支援普通滾輪橫向移動、拖曳橫向移動和 `Ctrl` + 滾輪縮放。連續點擊兩次可記錄 T1/T2 並顯示時間差。即使沒有偵測到返回脈衝，波形也會保留以便檢查接線、通道和音量。

## 國際化

程式啟動時會讀取系統 UI 語言。目前提供簡體中文、繁體中文、日語、韓語、西班牙語、法語、德語、義大利語、巴西葡萄牙語和俄語；沒有資源的語言會回退到英語。

## 發布

```powershell
.\publish.ps1
```

預設會產生以下自包含單檔程式：

```text
artifacts/publish/win-x86/EffectsLatencyTester_win-x86.exe
artifacts/publish/win-x64/EffectsLatencyTester_win-x64.exe
artifacts/publish/win-arm64/EffectsLatencyTester_win-arm64.exe
artifacts/publish/osx-x64/EffectsLatencyTester_osx-x64
artifacts/publish/osx-arm64/EffectsLatencyTester_osx-arm64
artifacts/publish/linux-x64/EffectsLatencyTester_linux-x64
artifacts/publish/linux-arm64/EffectsLatencyTester_linux-arm64
```

也可以使用 `-Runtime win-x64`、`-Runtime osx-arm64` 或 `-Runtime linux-x64` 只發布一個目標。目標電腦不需要安裝 .NET；Windows 需要匹配架構的 ASIO 驅動程式，macOS 和 Linux 透過 PortAudio 使用系統音訊介面。macOS/Linux 路徑仍需要在目標系統和真實聲卡上進一步驗證。

每個發布檔案的名稱格式為 `EffectsLatencyTester_<os-arch>`；Windows 目標使用 `.exe` 副檔名，macOS 和 Linux 不使用副檔名。

# 效果器延遲檢測（.NET 原型）

[English](../README.md) · [简体中文](README.zh-CN.md) · [繁體中文](README.zh-TW.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Español](README.es.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [Italiano](README.it.md) · [Português (Brasil)](README.pt-BR.md) · [Русский](README.ru.md)

這是一個 Windows WPF/.NET 8 原型，使用 `NAudio.Asio` 列出並開啟本機 ASIO 驅動程式，測量聲卡與效果器板的完整往返延遲。

## 執行

```powershell
dotnet restore
dotnet run --project .\LatencyTester.csproj
```

程式會優先選擇第一個可開啟的驅動程式，讀取取樣率與緩衝區，列出輸入/輸出通道，發送測試脈衝並偵測返回脈衝。測試後會顯示輸入、輸出和疊加波形及時間軸。

## 硬體連接

1. 將聲卡 ASIO 輸出連接到效果器板輸入。
2. 將效果器板輸出接回聲卡 ASIO 輸入。
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

預設會產生三個 Windows 自包含單檔程式：

```text
artifacts/publish/win-x86/LatencyTester.exe
artifacts/publish/win-x64/LatencyTester.exe
artifacts/publish/win-arm64/LatencyTester.exe
```

也可以使用 `-Runtime win-x64`、`-Runtime win-x86` 或 `-Runtime win-arm64` 只發布一種架構。目標電腦不需要安裝 .NET，但必須安裝相同架構的 ASIO 驅動程式。目前專案使用 WPF 和 `NAudio.Asio`，暫不支援 Linux/macOS。

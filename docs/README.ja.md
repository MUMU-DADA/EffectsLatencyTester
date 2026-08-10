# エフェクター・レイテンシー測定（.NET プロトタイプ）

[English](../README.md) · [简体中文](README.zh-CN.md) · [繁體中文](README.zh-TW.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Español](README.es.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [Italiano](README.it.md) · [Português (Brasil)](README.pt-BR.md) · [Русский](README.ru.md)

Avalonia を使用した .NET 8 デスクトップアプリで、オーディオインターフェイスとエフェクターボードの往復レイテンシーを測定します。Windows は ASIO、macOS は Core Audio、Linux は ALSA/PipeWire/JACK を PortAudio 経由で使用します。

## 実行

```powershell
dotnet restore
dotnet run --project .\EffectsLatencyTester.csproj
```

## スクリーンショット

![アプリケーションのスクリーンショット](Snipaste.png)

起動時には開ける最初のオーディオデバイスを優先し、サンプルレート、バッファー、入出力チャンネルを読み込みます。テストパルスを出力して戻りパルスを検出し、入力・出力・合成波形と時間軸を表示します。

## 接続

1. オーディオインターフェイスの オーディオ出力をエフェクターボードの入力へ接続します。
2. エフェクターボードの出力をインターフェイスの オーディオ入力へ戻します。
3. 直接モニタリングを無効にします。
4. 小さい音量からテストします。

## 測定

最初の測定値は、インターフェイス出力、エフェクターボード、インターフェイス入力を含む総往復レイテンシーです。エフェクターボードだけの遅延を求めるには、まず出力を入力へ直接接続して基準を測定し、その後エフェクターループを測定します。

```text
エフェクターボードの遅延 = ループの総遅延 - インターフェイス直結基準
```

波形表示では通常のホイールで横スクロール、ドラッグで横移動、`Ctrl` + ホイールでズームできます。曲線を2回クリックすると T1/T2 と時間差が表示されます。

## 多言語対応

起動時にシステム UI 言語を読み込みます。日本語、英語、中国語（簡体字・繁体字）、韓国語、スペイン語、フランス語、ドイツ語、イタリア語、ブラジルポルトガル語、ロシア語に対応し、未対応の言語は英語にフォールバックします。

## 公開

```powershell
.\publish.ps1
```

引数なしでは `win-x86`、`win-x64`、`win-arm64`、`osx-x64`、`osx-arm64`、`linux-x64`、`linux-arm64` 向けの自立型単一ファイルを生成します。特定のプラットフォームだけを生成する場合は、`-Runtime win-x64`、`-Runtime osx-arm64`、`-Runtime linux-x64` などを指定してください。対象コンピューターに .NET のインストールは不要です。Windows では対応する ASIO ドライバーが必要で、macOS と Linux では PortAudio 経由で各 OS のネイティブオーディオ API を使用します。macOS と Linux の経路は、実機での検証がまだ必要です。

公開ファイル名は `EffectsLatencyTester_<os-arch>` です。Windows には `.exe` 拡張子が付き、macOS と Linux には拡張子が付きません。

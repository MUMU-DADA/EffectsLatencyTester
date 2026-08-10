# Latenzmessung für Effektgeräte (.NET-Prototyp)

[English](../README.md) · [简体中文](README.zh-CN.md) · [繁體中文](README.zh-TW.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Español](README.es.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [Italiano](README.it.md) · [Português (Brasil)](README.pt-BR.md) · [Русский](README.ru.md)

Windows-WPF/.NET-8-Prototyp mit `NAudio.Asio`, der lokale ASIO-Treiber auflistet und öffnet und die gesamte Hin- und Rückweg-Latenz eines Audio-Interfaces und eines Effektgeräts misst.

## Ausführen

```powershell
dotnet restore
dotnet run --project .\EffectsLatencyTester.csproj
```

## Screenshot

![Application screenshot](Snipaste.png)

Die Anwendung wählt zuerst den ersten Treiber, der geöffnet werden kann, liest Abtastrate, Puffer und Kanäle, erzeugt Testimpulse, erkennt den Rückimpuls und zeigt Eingangs-, Ausgangs- und kombinierte Wellenformen an.

## Anschluss und Messung

Verbinden Sie den ASIO-Ausgang des Interfaces mit dem Eingang des Effektgeräts und dessen Ausgang mit einem ASIO-Eingang des Interfaces. Deaktivieren Sie Direct Monitoring und beginnen Sie mit geringer Lautstärke.

Die erste Messung ist die gesamte Hin- und Rückweg-Latenz. Für die Latenz des Effektgeräts messen Sie zuerst die Direktverbindungs-Basis mit direkt verbundenem Ausgang und Eingang und anschließend die Effektgerät-Schleife:

```text
Effektgerät-Latenz = Schleifen-Gesamtlatenz - Direktverbindungs-Basis
```

Die Wellenformansicht unterstützt horizontales Scrollen, Ziehen, Zoom mit `Ctrl` + Mausrad und zwei Klicks zum Aufzeichnen von T1/T2 und ihrer Zeitdifferenz.

## Sprachen

Beim Start wird die UI-Sprache des Systems gelesen. Verfügbar sind Deutsch, Englisch, vereinfachtes und traditionelles Chinesisch, Japanisch, Koreanisch, Spanisch, Französisch, Italienisch, brasilianisches Portugiesisch und Russisch. Nicht enthaltene Sprachen verwenden Englisch.

## Veröffentlichung

```powershell
.\publish.ps1
```

Ohne Parameter erzeugt das Skript eigenständige Einzeldateien für `win-x86`, `win-x64` und `win-arm64`. Für eine einzelne Architektur verwenden Sie `-Runtime win-x64`, `-Runtime win-x86` oder `-Runtime win-arm64`. .NET muss nicht installiert werden, der passende ASIO-Treiber ist jedoch erforderlich. Linux und macOS werden wegen WPF und `NAudio.Asio` derzeit nicht unterstützt.

# Latenzmessung für Effektgeräte (.NET-Prototyp)

[English](../README.md) · [简体中文](README.zh-CN.md) · [繁體中文](README.zh-TW.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Español](README.es.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [Italiano](README.it.md) · [Português (Brasil)](README.pt-BR.md) · [Русский](README.ru.md)

Dies ist eine .NET-8-Desktopanwendung mit Avalonia zur Messung der Hin- und Rückweg-Latenz eines Audio-Interfaces und eines Effektgeräts. Windows verwendet ASIO; macOS verwendet Core Audio, Linux PortAudio mit ALSA/PipeWire/JACK.

## Ausführen

```powershell
dotnet restore
dotnet run --project .\EffectsLatencyTester.csproj
```

## Screenshot

![Application screenshot](Snipaste.png)

Die Anwendung wählt zuerst das erste Audiogerät, das geöffnet werden kann, liest Abtastrate, Puffer und Kanäle, erzeugt Testimpulse, erkennt den Rückimpuls und zeigt Eingangs-, Ausgangs- und kombinierte Wellenformen an.

## Anschluss und Messung

Verbinden Sie den Audioausgang des Interfaces mit dem Eingang des Effektgeräts und dessen Ausgang mit einem Audioeingang des Interfaces. Deaktivieren Sie Direct Monitoring und beginnen Sie mit geringer Lautstärke.

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

Ohne Parameter erzeugt das Skript eigenständige Einzeldateien für `win-x86`, `win-x64`, `win-arm64`, `osx-x64`, `osx-arm64`, `linux-x64` und `linux-arm64`. Für eine einzelne Plattform verwenden Sie zum Beispiel `-Runtime win-x64`, `-Runtime osx-arm64` oder `-Runtime linux-x64`. .NET muss auf dem Zielcomputer nicht installiert werden. Windows benötigt einen passenden ASIO-Treiber; macOS und Linux verwenden die jeweiligen nativen Audio-Host-APIs über PortAudio. Die macOS- und Linux-Pfade müssen noch mit echter Hardware auf den Zielsystemen validiert werden.

Jede veröffentlichte Datei heißt `EffectsLatencyTester_<os-arch>`; Windows verwendet die Endung `.exe`, macOS und Linux keine Dateiendung.

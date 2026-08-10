# Misuratore di latenza degli effetti (prototipo .NET)

[English](../README.md) · [简体中文](README.zh-CN.md) · [繁體中文](README.zh-TW.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Español](README.es.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [Italiano](README.it.md) · [Português (Brasil)](README.pt-BR.md) · [Русский](README.ru.md)

Applicazione desktop .NET 8 con Avalonia per misurare la latenza completa di andata e ritorno di un’interfaccia audio e di una pedaliera. Windows usa ASIO; macOS usa Core Audio e Linux usa PortAudio con ALSA/PipeWire/JACK.

## Esecuzione

```powershell
dotnet restore
dotnet run --project .\EffectsLatencyTester.csproj
```

## Schermata

![Schermata dell'applicazione](Snipaste.png)

L’app seleziona prima il dispositivo audio che può essere aperto, legge frequenza di campionamento, buffer e canali, genera impulsi di prova, rileva l’impulso di ritorno e mostra le forme d’onda di ingresso, uscita e combinate.

## Collegamento e misurazione

Collega l’uscita audio dell’interfaccia all’ingresso della pedaliera e l’uscita della pedaliera a un ingresso audio dell’interfaccia. Disattiva il Direct Monitoring e inizia con un volume basso.

La prima misura è la latenza totale di andata e ritorno. Per isolare la latenza della pedaliera, misura prima il riferimento collegando direttamente uscita e ingresso dell’interfaccia, quindi misura il loop:

```text
Latenza della pedaliera = Latenza totale del loop - Riferimento dell’interfaccia diretta
```

La vista delle forme d’onda supporta lo scorrimento orizzontale, il trascinamento, lo zoom con `Ctrl` + rotellina e due clic per registrare T1/T2 e calcolare la differenza.

## Lingue

All’avvio l’app legge la lingua dell’interfaccia di sistema. Sono disponibili italiano, inglese, cinese semplificato e tradizionale, giapponese, coreano, spagnolo, francese, tedesco, portoghese brasiliano e russo. Le lingue non incluse usano l’inglese.

## Pubblicazione

```powershell
.\publish.ps1
```

Senza parametri lo script genera file singoli autonomi per `win-x86`, `win-x64`, `win-arm64`, `osx-x64`, `osx-arm64`, `linux-x64` e `linux-arm64`. Per una sola piattaforma usa, ad esempio, `-Runtime win-x64`, `-Runtime osx-arm64` o `-Runtime linux-x64`. .NET non deve essere installato. Windows richiede il driver ASIO corrispondente; macOS e Linux usano le API audio native tramite PortAudio. I percorsi macOS e Linux devono ancora essere verificati con hardware reale sui sistemi di destinazione.

Ogni file pubblicato usa il nome `EffectsLatencyTester_<os-arch>`; i target Windows hanno l’estensione `.exe`, macOS e Linux nessuna estensione.

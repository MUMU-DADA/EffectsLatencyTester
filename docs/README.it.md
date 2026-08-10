# Misuratore di latenza degli effetti (prototipo .NET)

[English](../README.md) · [简体中文](README.zh-CN.md) · [繁體中文](README.zh-TW.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Español](README.es.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [Italiano](README.it.md) · [Português (Brasil)](README.pt-BR.md) · [Русский](README.ru.md)

Prototipo Windows WPF/.NET 8 che usa `NAudio.Asio` per elencare e aprire i driver ASIO locali e misurare la latenza completa di andata e ritorno di un’interfaccia audio e di una pedaliera.

## Esecuzione

```powershell
dotnet restore
dotnet run --project .\LatencyTester.csproj
```

L’app seleziona prima il driver che può essere aperto, legge frequenza di campionamento, buffer e canali, genera impulsi di prova, rileva l’impulso di ritorno e mostra le forme d’onda di ingresso, uscita e combinate.

## Collegamento e misurazione

Collega l’uscita ASIO dell’interfaccia all’ingresso della pedaliera e l’uscita della pedaliera a un ingresso ASIO dell’interfaccia. Disattiva il Direct Monitoring e inizia con un volume basso.

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

Senza parametri lo script genera file singoli autonomi per `win-x86`, `win-x64` e `win-arm64`. Per una sola architettura usa `-Runtime win-x64`, `-Runtime win-x86` o `-Runtime win-arm64`. .NET non deve essere installato, ma serve il driver ASIO corrispondente. Linux e macOS non sono attualmente supportati perché il progetto usa WPF e `NAudio.Asio`.

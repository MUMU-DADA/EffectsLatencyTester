# Medidor de latência de efeitos (protótipo .NET)

[English](../README.md) · [简体中文](README.zh-CN.md) · [繁體中文](README.zh-TW.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Español](README.es.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [Italiano](README.it.md) · [Português (Brasil)](README.pt-BR.md) · [Русский](README.ru.md)

Aplicativo de desktop .NET 8 com Avalonia para medir a latência total de ida e volta de uma interface de áudio e uma pedaleira. O Windows usa ASIO; o macOS usa Core Audio e o Linux usa PortAudio com ALSA/PipeWire/JACK.

## Execução

```powershell
dotnet restore
dotnet run --project .\EffectsLatencyTester.csproj
```

## Captura de tela

![Captura de tela do aplicativo](Snipaste.png)

O aplicativo seleciona primeiro o dispositivo de áudio que pode ser aberto, lê taxa de amostragem, buffer e canais, gera pulsos de teste, detecta o pulso de retorno e exibe as formas de onda de entrada, saída e combinada.

## Conexão e medição

Conecte a saída de áudio da interface à entrada da pedaleira e a saída da pedaleira a uma entrada de audio da interface. Desative o Direct Monitoring e comece com volume baixo.

A primeira medição é a latência total de ida e volta. Para isolar a latência da pedaleira, meça primeiro a referência conectando diretamente a saída à entrada da interface e depois meça o loop:

```text
Latência da pedaleira = Latência total do loop - Referência da interface direta
```

A visualização de formas de onda permite rolagem horizontal, arraste, zoom com `Ctrl` + roda do mouse e dois cliques para registrar T1/T2 e calcular a diferença.

## Idiomas

Na inicialização, o aplicativo lê o idioma da interface do sistema. Há recursos em português do Brasil, inglês, chinês simplificado e tradicional, japonês, coreano, espanhol, francês, alemão, italiano e russo. Idiomas não incluídos usam inglês.

## Publicação

```powershell
.\publish.ps1
```

Sem parâmetros, o script gera arquivos únicos autocontidos para `win-x86`, `win-x64`, `win-arm64`, `osx-x64`, `osx-arm64`, `linux-x64` e `linux-arm64`. Para uma plataforma específica, use por exemplo `-Runtime win-x64`, `-Runtime osx-arm64` ou `-Runtime linux-x64`. Não é necessário instalar o .NET. O Windows requer o driver ASIO correspondente; macOS e Linux usam suas APIs de áudio nativas por meio do PortAudio. Os caminhos de macOS e Linux ainda precisam ser validados com hardware real nos sistemas de destino.

Cada arquivo publicado usa o nome `EffectsLatencyTester_<os-arch>`; os destinos Windows usam `.exe`, enquanto macOS e Linux não usam extensão.

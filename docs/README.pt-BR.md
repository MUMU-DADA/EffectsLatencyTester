# Medidor de latência de efeitos (protótipo .NET)

[English](../README.md) · [简体中文](README.zh-CN.md) · [繁體中文](README.zh-TW.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Español](README.es.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [Italiano](README.it.md) · [Português (Brasil)](README.pt-BR.md) · [Русский](README.ru.md)

Protótipo Windows WPF/.NET 8 que usa `NAudio.Asio` para listar e abrir drivers ASIO locais e medir a latência total de ida e volta de uma interface de áudio e de uma pedaleira.

## Execução

```powershell
dotnet restore
dotnet run --project .\LatencyTester.csproj
```

O aplicativo seleciona primeiro o driver que pode ser aberto, lê taxa de amostragem, buffer e canais, gera pulsos de teste, detecta o pulso de retorno e exibe as formas de onda de entrada, saída e combinada.

## Conexão e medição

Conecte a saída ASIO da interface à entrada da pedaleira e a saída da pedaleira a uma entrada ASIO da interface. Desative o Direct Monitoring e comece com volume baixo.

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

Sem parâmetros, o script gera arquivos únicos autocontidos para `win-x86`, `win-x64` e `win-arm64`. Para uma arquitetura específica, use `-Runtime win-x64`, `-Runtime win-x86` ou `-Runtime win-arm64`. Não é necessário instalar o .NET, mas o driver ASIO correspondente é necessário. Linux e macOS ainda não são compatíveis porque o projeto usa WPF e `NAudio.Asio`.

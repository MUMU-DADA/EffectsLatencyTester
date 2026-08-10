# Medidor de latencia de pedales de efectos (prototipo .NET)

[English](../README.md) · [简体中文](README.zh-CN.md) · [繁體中文](README.zh-TW.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Español](README.es.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [Italiano](README.it.md) · [Português (Brasil)](README.pt-BR.md) · [Русский](README.ru.md)

Aplicación de escritorio .NET 8 con Avalonia para medir la latencia total de ida y vuelta de una interfaz de audio y una pedalera. Windows usa ASIO; macOS usa Core Audio y Linux usa PortAudio con ALSA/PipeWire/JACK.

## Ejecución

```powershell
dotnet restore
dotnet run --project .\EffectsLatencyTester.csproj
```

## Captura de pantalla

![Captura de pantalla de la aplicación](Snipaste.png)

La aplicación selecciona primero el dispositivo de audio que se pueda abrir, lee la frecuencia de muestreo, el búfer y los canales, genera pulsos de prueba, detecta el pulso de retorno y muestra las formas de onda de entrada, salida y combinada.

## Conexión y medición

Conecta la salida de audio de la interfaz a la entrada de la pedalera y la salida de la pedalera a una entrada de audio de la interfaz. Desactiva Direct Monitoring y empieza con un volumen bajo.

La primera medición es la latencia total de ida y vuelta. Para obtener la latencia de la pedalera, mide primero la interfaz conectando directamente su salida a la entrada y después mide el bucle:

```text
Latencia de la pedalera = Latencia total del bucle - Referencia de la interfaz directa
```

La vista de formas de onda permite desplazamiento horizontal, arrastre, zoom con `Ctrl` + rueda y dos clics para registrar T1/T2 y calcular la diferencia.

## Idiomas

La aplicación lee el idioma de la interfaz del sistema al iniciar. Incluye español, inglés, chino simplificado y tradicional, japonés, coreano, francés, alemán, italiano, portugués de Brasil y ruso. Los idiomas no incluidos usan inglés.

## Publicación

```powershell
.\publish.ps1
```

Sin parámetros genera archivos únicos autocontenidos para `win-x86`, `win-x64`, `win-arm64`, `osx-x64`, `osx-arm64`, `linux-x64` y `linux-arm64`. Para una sola plataforma, usa por ejemplo `-Runtime win-x64`, `-Runtime osx-arm64` o `-Runtime linux-x64`. No hace falta instalar .NET. Windows requiere el controlador ASIO correspondiente; macOS y Linux usan sus APIs de audio nativas mediante PortAudio. Las rutas de macOS y Linux aún deben validarse con hardware real en los sistemas de destino.

Cada archivo publicado se llama `EffectsLatencyTester_<os-arch>`; los destinos Windows usan `.exe` y macOS/Linux no llevan extensión.

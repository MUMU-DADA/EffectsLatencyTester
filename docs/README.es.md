# Medidor de latencia de pedales de efectos (prototipo .NET)

[English](../README.md) · [简体中文](README.zh-CN.md) · [繁體中文](README.zh-TW.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Español](README.es.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [Italiano](README.it.md) · [Português (Brasil)](README.pt-BR.md) · [Русский](README.ru.md)

Prototipo Windows WPF/.NET 8 que usa `NAudio.Asio` para listar y abrir controladores ASIO locales y medir la latencia total de ida y vuelta de una interfaz de audio y una pedalera.

## Ejecución

```powershell
dotnet restore
dotnet run --project .\EffectsLatencyTester.csproj
```

## Captura de pantalla

![Captura de pantalla de la aplicación](Snipaste.png)

La aplicación selecciona primero el controlador que se pueda abrir, lee la frecuencia de muestreo, el búfer y los canales, genera pulsos de prueba, detecta el pulso de retorno y muestra las formas de onda de entrada, salida y combinada.

## Conexión y medición

Conecta la salida ASIO de la interfaz a la entrada de la pedalera y la salida de la pedalera a una entrada ASIO de la interfaz. Desactiva Direct Monitoring y empieza con un volumen bajo.

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

Sin parámetros genera archivos únicos autocontenidos para `win-x86`, `win-x64` y `win-arm64`. Para una sola arquitectura, usa `-Runtime win-x64`, `-Runtime win-x86` o `-Runtime win-arm64`. No hace falta instalar .NET, pero sí el controlador ASIO correspondiente. Linux y macOS no están disponibles porque el proyecto usa WPF y `NAudio.Asio`.

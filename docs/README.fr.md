# Mesure de latence des effets (prototype .NET)

[English](../README.md) · [简体中文](README.zh-CN.md) · [繁體中文](README.zh-TW.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Español](README.es.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [Italiano](README.it.md) · [Português (Brasil)](README.pt-BR.md) · [Русский](README.ru.md)

Prototype Windows WPF/.NET 8 utilisant `NAudio.Asio` pour lister et ouvrir les pilotes ASIO locaux et mesurer la latence aller-retour complète d’une interface audio et d’un pédalier d’effets.

## Exécution

```powershell
dotnet restore
dotnet run --project .\EffectsLatencyTester.csproj
```

L’application sélectionne d’abord le pilote qui peut être ouvert, lit la fréquence, le tampon et les canaux, génère des impulsions de test, détecte l’impulsion de retour et affiche les formes d’onde d’entrée, de sortie et combinée.

## Connexion et mesure

Reliez la sortie ASIO de l’interface à l’entrée du pédalier, puis la sortie du pédalier à une entrée ASIO de l’interface. Désactivez le Direct Monitoring et commencez avec un volume faible.

La première mesure est la latence aller-retour totale. Pour isoler la latence du pédalier, mesurez d’abord la référence avec la sortie de l’interface reliée directement à son entrée, puis mesurez la boucle :

```text
Latence du pédalier = Latence totale de la boucle - Référence de l’interface directe
```

La vue des formes d’onde permet le défilement horizontal, le déplacement par glisser-déposer, le zoom avec `Ctrl` + molette et deux clics pour enregistrer T1/T2 et calculer l’écart.

## Langues

L’application lit la langue d’interface du système au démarrage. Elle fournit le français, l’anglais, le chinois simplifié et traditionnel, le japonais, le coréen, l’espagnol, l’allemand, l’italien, le portugais du Brésil et le russe. Les langues non incluses utilisent l’anglais.

## Publication

```powershell
.\publish.ps1
```

Sans paramètre, le script génère des fichiers uniques autonomes pour `win-x86`, `win-x64` et `win-arm64`. Pour une seule architecture, utilisez `-Runtime win-x64`, `-Runtime win-x86` ou `-Runtime win-arm64`. .NET n’a pas besoin d’être installé, mais le pilote ASIO correspondant reste nécessaire. Linux et macOS ne sont pas pris en charge car le projet utilise WPF et `NAudio.Asio`.

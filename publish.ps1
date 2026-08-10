[CmdletBinding()]
param(
    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release',
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'

$projectPath = Join-Path $PSScriptRoot 'LatencyTester.csproj'
$iconPath = Join-Path $PSScriptRoot 'Assets\LatencyTesterIcon.ico'
$iconGeneratorPath = Join-Path $PSScriptRoot 'tools\GenerateIcon.ps1'
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $PSScriptRoot 'artifacts\publish'
}
$outputFullPath = [System.IO.Path]::GetFullPath($OutputPath)

if (-not (Test-Path -LiteralPath $iconPath)) {
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $iconGeneratorPath
    if ($LASTEXITCODE -ne 0) {
        throw "Icon generation failed with exit code $LASTEXITCODE."
    }
}

New-Item -ItemType Directory -Path $outputFullPath -Force | Out-Null

$publishArguments = @(
    $projectPath,
    '--configuration', $Configuration,
    '--framework', 'net8.0-windows',
    '--runtime', 'win-x64',
    '--self-contained', 'true',
    '--output', $outputFullPath,
    '-p:PublishSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:IncludeAllContentForSelfExtract=true',
    '-p:EnableCompressionInSingleFile=true',
    '-p:PublishTrimmed=false',
    '-p:DebugType=None',
    '-p:DebugSymbols=false'
)

Write-Host "Publishing self-contained single-file application to $outputFullPath ..."
& dotnet publish @publishArguments
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$executablePath = Join-Path $outputFullPath 'LatencyTester.exe'
if (-not (Test-Path -LiteralPath $executablePath)) {
    throw "Publish completed but the expected executable was not found: $executablePath"
}

$publishedFiles = @(Get-ChildItem -LiteralPath $outputFullPath -File)
Write-Host "Published executable: $executablePath"
Write-Host ("Output files: " + ($publishedFiles.Name -join ', '))

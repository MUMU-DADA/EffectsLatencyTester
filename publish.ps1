[CmdletBinding()]
param(
    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release',
    [ValidateSet('All', 'win-x86', 'win-x64', 'win-arm64')]
    [string]$Runtime = 'All',
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'

$projectPath = Join-Path $PSScriptRoot 'EffectsLatencyTester.csproj'
$iconPath = Join-Path $PSScriptRoot 'Assets\EffectsLatencyTesterIcon.ico'
$iconGeneratorPath = Join-Path $PSScriptRoot 'tools\GenerateIcon.ps1'
$projectBaseName = [System.IO.Path]::GetFileNameWithoutExtension($projectPath)
$temporaryProjectPattern = "${projectBaseName}_*_wpftmp.csproj"
$hasExplicitOutputPath = -not [string]::IsNullOrWhiteSpace($OutputPath)
$outputRoot = if ($hasExplicitOutputPath) {
    [System.IO.Path]::GetFullPath($OutputPath)
}
else {
    Join-Path $PSScriptRoot 'artifacts\publish'
}

$runtimes = if ($Runtime -eq 'All') {
    @('win-x86', 'win-x64', 'win-arm64')
}
else {
    @($Runtime)
}

function Remove-WpfTemporaryProjects {
    $temporaryProjects = @(Get-ChildItem -LiteralPath $PSScriptRoot -Filter $temporaryProjectPattern -File -ErrorAction SilentlyContinue)
    foreach ($temporaryProject in $temporaryProjects) {
        try {
            Remove-Item -LiteralPath $temporaryProject.FullName -Force -ErrorAction Stop
            Write-Host "Removed WPF temporary project: $($temporaryProject.Name)"
        }
        catch {
            Write-Warning "Could not remove WPF temporary project '$($temporaryProject.FullName)': $($_.Exception.Message)"
        }
    }
}

Remove-WpfTemporaryProjects

if (-not (Test-Path -LiteralPath $iconPath)) {
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $iconGeneratorPath
    if ($LASTEXITCODE -ne 0) {
        throw "Icon generation failed with exit code $LASTEXITCODE."
    }
}

function Publish-Runtime {
    param(
        [Parameter(Mandatory)]
        [string]$TargetRuntime,
        [Parameter(Mandatory)]
        [string]$TargetOutputPath
    )

    $platformTarget = switch ($TargetRuntime) {
        'win-x86' { 'x86' }
        'win-x64' { 'x64' }
        'win-arm64' { 'arm64' }
        default { throw "Unsupported Windows runtime: $TargetRuntime" }
    }

    New-Item -ItemType Directory -Path $TargetOutputPath -Force | Out-Null

    $publishArguments = @(
        '-nr:false',
        $projectPath,
        '--configuration', $Configuration,
        '--framework', 'net8.0-windows',
        '--runtime', $TargetRuntime,
        '--self-contained', 'true',
        '--output', $TargetOutputPath,
        "-p:PlatformTarget=$platformTarget",
        "-p:Platforms=$platformTarget",
        '-p:PublishSingleFile=true',
        '-p:IncludeNativeLibrariesForSelfExtract=true',
        '-p:IncludeAllContentForSelfExtract=true',
        '-p:EnableCompressionInSingleFile=true',
        '-p:PublishTrimmed=false',
        '-p:DebugType=None',
        '-p:DebugSymbols=false'
    )

    Write-Host "Publishing self-contained single-file application for $TargetRuntime to $TargetOutputPath ..."
    & dotnet publish @publishArguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed for $TargetRuntime with exit code $LASTEXITCODE."
    }

    $executablePath = Join-Path $TargetOutputPath 'EffectsLatencyTester.exe'
    if (-not (Test-Path -LiteralPath $executablePath)) {
        throw "Publish completed but the expected executable was not found: $executablePath"
    }

    $publishedFiles = @(Get-ChildItem -LiteralPath $TargetOutputPath -File)
    Write-Host "Published executable: $executablePath"
    Write-Host ("Output files: " + ($publishedFiles.Name -join ', '))
}

$localDotnetHome = Join-Path $PSScriptRoot '.dotnet-home'
$localNugetPackages = Join-Path $PSScriptRoot '.nuget-packages'
$previousDotnetCliHome = $env:DOTNET_CLI_HOME
$previousNugetPackages = $env:NUGET_PACKAGES

# Keep the CLI state and NuGet package cache local to this project while publishing.
New-Item -ItemType Directory -Path $localDotnetHome, $localNugetPackages -Force | Out-Null
$env:DOTNET_CLI_HOME = $localDotnetHome
$env:NUGET_PACKAGES = $localNugetPackages
try {
    foreach ($targetRuntime in $runtimes) {
        $targetOutputPath = if ($Runtime -eq 'All' -or -not $hasExplicitOutputPath) {
            Join-Path $outputRoot $targetRuntime
        }
        else {
            $outputRoot
        }

        Publish-Runtime -TargetRuntime $targetRuntime -TargetOutputPath $targetOutputPath
    }
}
finally {
    Remove-WpfTemporaryProjects

    # Do not leak the publish script's project-local environment into the caller.
    $env:DOTNET_CLI_HOME = $previousDotnetCliHome
    $env:NUGET_PACKAGES = $previousNugetPackages
}

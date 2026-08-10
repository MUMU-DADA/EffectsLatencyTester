param(
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\Assets\EffectsLatencyTesterIcon.ico')
)

$ErrorActionPreference = 'Stop'

$outputFullPath = [System.IO.Path]::GetFullPath($OutputPath)
$outputDirectory = Split-Path -Parent $outputFullPath
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null

Add-Type -AssemblyName System.Drawing

function New-PixelIconPng {
    param([int]$Size)

    $bitmap = [System.Drawing.Bitmap]::new(
        $Size,
        $Size,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $pngStream = [System.IO.MemoryStream]::new()

    try {
        $graphics.Clear([System.Drawing.Color]::FromArgb(255, 11, 17, 24))
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::None
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half

        $cell = [Math]::Max(1, [Math]::Floor($Size / 32))
        $pixel = {
            param([int]$X, [int]$Y, [int]$Width, [int]$Height, [System.Drawing.Brush]$Brush)
            $graphics.FillRectangle($Brush, $X * $cell, $Y * $cell, $Width * $cell, $Height * $cell)
        }

        $borderBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 22, 35, 47))
        $innerBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 16, 26, 36))
        $cyanBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 0, 229, 255))
        $magentaBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 255, 65, 108))
        $markerBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 255, 224, 102))

        try {
            & $pixel 2 2 28 28 $borderBrush
            & $pixel 4 4 24 24 $innerBrush

            & $pixel 4 15 3 2 $cyanBrush
            & $pixel 7 12 3 8 $cyanBrush
            & $pixel 10 8 3 16 $cyanBrush
            & $pixel 13 14 3 4 $cyanBrush
            & $pixel 16 16 3 2 $cyanBrush

            & $pixel 17 15 3 2 $magentaBrush
            & $pixel 20 11 3 10 $magentaBrush
            & $pixel 23 7 3 18 $magentaBrush
            & $pixel 26 14 2 4 $magentaBrush

            & $pixel 14 13 2 6 $markerBrush
        }
        finally {
            $borderBrush.Dispose()
            $innerBrush.Dispose()
            $cyanBrush.Dispose()
            $magentaBrush.Dispose()
            $markerBrush.Dispose()
        }

        $bitmap.Save($pngStream, [System.Drawing.Imaging.ImageFormat]::Png)
        return ,$pngStream.ToArray()
    }
    finally {
        $pngStream.Dispose()
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

$sizes = @(32, 256)
$frames = @($sizes | ForEach-Object { New-PixelIconPng -Size $_ })
$iconStream = [System.IO.MemoryStream]::new()
$writer = [System.IO.BinaryWriter]::new($iconStream)

try {
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$frames.Count)

    $imageOffset = 6 + (16 * $frames.Count)
    for ($index = 0; $index -lt $frames.Count; $index++) {
        $size = $sizes[$index]
        $frame = [byte[]]$frames[$index]
        $writer.Write([byte]$(if ($size -ge 256) { 0 } else { $size }))
        $writer.Write([byte]$(if ($size -ge 256) { 0 } else { $size }))
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$frame.Length)
        $writer.Write([uint32]$imageOffset)
        $imageOffset += $frame.Length
    }

    foreach ($frame in $frames) {
        $writer.Write([byte[]]$frame)
    }

    [System.IO.File]::WriteAllBytes($outputFullPath, $iconStream.ToArray())
}
finally {
    $writer.Dispose()
    $iconStream.Dispose()
}

Write-Host "Generated pixel icon: $outputFullPath"

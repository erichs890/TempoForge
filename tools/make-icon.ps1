# Gera o ícone do TempoForge (desenho próprio: três barras em brasa sobre fundo de carvão)
# em um .ico com vários tamanhos. Uso:  .\tools\make-icon.ps1
param([string]$Output = '')
$ErrorActionPreference = 'Stop'
if (-not $Output) {
    $here = Split-Path -Parent $MyInvocation.MyCommand.Path
    $Output = Join-Path $here '..\src\TempoForge.App\Assets\app.ico'
}
Add-Type -AssemblyName System.Drawing

function New-RoundedRect([single]$x, [single]$y, [single]$w, [single]$h, [single]$r) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $path.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-IconPng([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)
    $s = $size / 64.0

    $bg = New-RoundedRect (2 * $s) (2 * $s) (60 * $s) (60 * $s) (14 * $s)
    $bgBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.PointF 0, 0), (New-Object System.Drawing.PointF 0, $size),
        [System.Drawing.Color]::FromArgb(255, 35, 40, 51), [System.Drawing.Color]::FromArgb(255, 15, 17, 21))
    $g.FillPath($bgBrush, $bg)

    $barBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.PointF 0, (12 * $s)), (New-Object System.Drawing.PointF 0, (52 * $s)),
        [System.Drawing.Color]::FromArgb(255, 255, 170, 85), [System.Drawing.Color]::FromArgb(255, 255, 98, 36))
    # x, topo, altura de cada barra numa grade de 64
    foreach ($bar in @(@(14, 28, 22), @(27, 13, 37), @(40, 21, 29))) {
        $p = New-RoundedRect ($bar[0] * $s) ($bar[1] * $s) (10 * $s) ($bar[2] * $s) (5 * $s)
        $g.FillPath($barBrush, $p)
        $p.Dispose()
    }

    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return , $ms.ToArray()
}

$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$images = $sizes | ForEach-Object { , (New-IconPng $_) }

New-Item -ItemType Directory -Force (Split-Path -Parent $Output) | Out-Null
$out = [System.IO.File]::Create($Output)
$w = New-Object System.IO.BinaryWriter $out
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$images[$i].Length); $w.Write([uint32]$offset)
    $offset += $images[$i].Length
}
foreach ($img in $images) { $w.Write($img) }
$w.Close()
Get-Item $Output | Select-Object Name, Length

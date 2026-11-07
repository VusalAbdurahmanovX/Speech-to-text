$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$assets = Join-Path (Split-Path $PSScriptRoot) 'src\SubtitleStudio\Assets'
New-Item -ItemType Directory -Force $assets | Out-Null
$bars = @(@(112,224,22,64), @(152,192,22,128), @(192,160,22,192), @(232,200,22,112), @(292,185,108,22), @(292,245,86,22), @(292,305,108,22))
$svg = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 512 512"><title>Speech to Text</title><defs><linearGradient id="blue" x2="1" y2="1"><stop stop-color="#367BF5"/><stop offset="1" stop-color="#1747B2"/></linearGradient></defs><rect width="512" height="512" rx="112" fill="url(#blue)"/>'
foreach ($bar in $bars) { $svg += '<rect x="{0}" y="{1}" width="{2}" height="{3}" rx="11" fill="white"/>' -f $bar[0],$bar[1],$bar[2],$bar[3] }
[IO.File]::WriteAllText((Join-Path $assets 'AppIcon.svg'), $svg + '</svg>' + "`n", [Text.UTF8Encoding]::new($false))
function Fill-RoundedRect($graphics, $brush, [float]$x, [float]$y, [float]$width, [float]$height, [float]$radius) {
    $path = [Drawing.Drawing2D.GraphicsPath]::new()
    $diameter = $radius * 2
    $path.AddArc($x,$y,$diameter,$diameter,180,90)
    $path.AddArc($x+$width-$diameter,$y,$diameter,$diameter,270,90)
    $path.AddArc($x+$width-$diameter,$y+$height-$diameter,$diameter,$diameter,0,90)
    $path.AddArc($x,$y+$height-$diameter,$diameter,$diameter,90,90)
    $path.CloseFigure(); $graphics.FillPath($brush,$path); $path.Dispose()
}
$images = @()
foreach ($size in @(16,24,32,48,64,128,256)) {
    $bitmap = [Drawing.Bitmap]::new($size,$size)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.ScaleTransform($size/512.0,$size/512.0)
    $gradient = [Drawing.Drawing2D.LinearGradientBrush]::new([Drawing.Rectangle]::new(0,0,512,512),[Drawing.ColorTranslator]::FromHtml('#367BF5'),[Drawing.ColorTranslator]::FromHtml('#1747B2'),[single]45)
    Fill-RoundedRect $graphics $gradient 0 0 512 512 112
    foreach ($bar in $bars) { Fill-RoundedRect $graphics ([Drawing.Brushes]::White) $bar[0] $bar[1] $bar[2] $bar[3] 11 }
    $stream = [IO.MemoryStream]::new(); $bitmap.Save($stream,[Drawing.Imaging.ImageFormat]::Png)
    $images += ,@{ Size=$size; Bytes=$stream.ToArray() }
    if ($size -eq 256) { [IO.File]::WriteAllBytes((Join-Path $assets 'AppIcon.png'),$stream.ToArray()) }
    $stream.Dispose(); $gradient.Dispose(); $graphics.Dispose(); $bitmap.Dispose()
}
$writer = [IO.BinaryWriter]::new([IO.File]::Create((Join-Path $assets 'AppIcon.ico')))
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$images.Count)
    $offset = 6 + 16 * $images.Count
    foreach ($icon in $images) {
        $dimension = [byte]($icon.Size % 256)
        $writer.Write($dimension); $writer.Write($dimension); $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$icon.Bytes.Length); $writer.Write([uint32]$offset)
        $offset += $icon.Bytes.Length
    }
    foreach ($icon in $images) { $writer.Write([byte[]]$icon.Bytes) }
} finally { $writer.Dispose() }
Write-Host "Generated SVG, PNG, and seven-size Windows ICO in $assets"

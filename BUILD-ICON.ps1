# SPDX-License-Identifier: MIT
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore,PresentationFramework,WindowsBase
$assetRoot = Join-Path $PSScriptRoot 'assets'
New-Item -ItemType Directory -Force $assetRoot | Out-Null
$drawingMarkup = @'
<DrawingGroup xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
  <GeometryDrawing Geometry="M64,8 H192 A56,56 0 0 1 248,64 V192 A56,56 0 0 1 192,248 H64 A56,56 0 0 1 8,192 V64 A56,56 0 0 1 64,8 Z">
    <GeometryDrawing.Brush><LinearGradientBrush StartPoint="0,0" EndPoint="1,1"><GradientStop Color="#1685ef" Offset="0"/><GradientStop Color="#0754bd" Offset="1"/></LinearGradientBrush></GeometryDrawing.Brush>
  </GeometryDrawing>
  <GeometryDrawing Geometry="M181,76 A75,75 0 1 0 190,163">
    <GeometryDrawing.Pen><Pen Brush="White" Thickness="18" StartLineCap="Round" EndLineCap="Round"/></GeometryDrawing.Pen>
  </GeometryDrawing>
  <GeometryDrawing Brush="White" Geometry="M111,94 L159,128 L111,162 Z">
    <GeometryDrawing.Pen><Pen Brush="White" Thickness="6" LineJoin="Round"/></GeometryDrawing.Pen>
  </GeometryDrawing>
  <GeometryDrawing Brush="#82f2d2" Geometry="M199,96 H209 A6,6 0 0 1 215,102 V112 A6,6 0 0 1 209,118 H199 A6,6 0 0 1 193,112 V102 A6,6 0 0 1 199,96 Z"/>
</DrawingGroup>
'@
$drawing = [Windows.Markup.XamlReader]::Parse($drawingMarkup)
$frames = @()
foreach ($size in @(16,24,32,48,64,128,256,1024)) {
    $visual = [Windows.Media.DrawingVisual]::new()
    $context = $visual.RenderOpen()
    $context.PushTransform([Windows.Media.ScaleTransform]::new($size / 256.0,$size / 256.0))
    $context.DrawDrawing($drawing)
    $context.Pop()
    $context.Close()
    $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new($size,$size,96,96,[Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = [IO.MemoryStream]::new()
    $encoder.Save($stream)
    $pngBytes = $stream.ToArray()
    [IO.File]::WriteAllBytes((Join-Path $assetRoot "pclo-$size.png"),$pngBytes)
    if ($size -le 256) { $frames += @{ Size=$size; Bytes=$pngBytes } }
    $stream.Dispose()
}
$icoPath = Join-Path $assetRoot 'pclo.ico'
$icoStream = [IO.File]::Create($icoPath)
$writer = [IO.BinaryWriter]::new($icoStream)
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frames.Count)
$offset = 6 + 16 * $frames.Count
foreach ($frame in $frames) {
    $dimension = if ($frame.Size -eq 256) { 0 } else { $frame.Size }
    $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
    $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([uint16]1); $writer.Write([uint16]32)
    $writer.Write([uint32]$frame.Bytes.Length); $writer.Write([uint32]$offset)
    $offset += $frame.Bytes.Length
}
foreach ($frame in $frames) { $writer.Write([byte[]]$frame.Bytes) }
$writer.Dispose()
Write-Host "Icon generated: $icoPath"

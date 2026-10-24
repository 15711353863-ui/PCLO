# SPDX-License-Identifier: MIT
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore,PresentationFramework,WindowsBase
$assetRoot = Join-Path $PSScriptRoot 'assets'
New-Item -ItemType Directory -Force $assetRoot | Out-Null
# Convert the saved imagegen edit into application sizes without regenerating artwork.
$sourceStream = [IO.File]::OpenRead((Join-Path $assetRoot 'pclo-source.png'))
$decoder = [Windows.Media.Imaging.PngBitmapDecoder]::new($sourceStream,[Windows.Media.Imaging.BitmapCreateOptions]::PreservePixelFormat,[Windows.Media.Imaging.BitmapCacheOption]::OnLoad)
$sourceBitmap = $decoder.Frames[0]
$sourceStream.Dispose()
$frames = @()
foreach ($size in @(16,24,32,48,64,128,256,1024)) {
    $visual = [Windows.Media.DrawingVisual]::new()
    [Windows.Media.RenderOptions]::SetBitmapScalingMode($visual,[Windows.Media.BitmapScalingMode]::HighQuality)
    $context = $visual.RenderOpen()
    $scale = ($size * 0.94) / [Math]::Max($sourceBitmap.PixelWidth,$sourceBitmap.PixelHeight)
    $width = $sourceBitmap.PixelWidth * $scale
    $height = $sourceBitmap.PixelHeight * $scale
    $context.DrawImage($sourceBitmap,[Windows.Rect]::new(($size-$width)/2,($size-$height)/2,$width,$height))
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
Copy-Item $icoPath (Join-Path $PSScriptRoot 'Plain Craft Launcher 2\Images\pclo.ico') -Force
Copy-Item (Join-Path $assetRoot 'pclo-256.png') (Join-Path $PSScriptRoot 'Plain Craft Launcher 2\Images\Heads\PCLO.png') -Force
Copy-Item (Join-Path $assetRoot 'pclo-256.png') (Join-Path $PSScriptRoot 'Plain Craft Launcher 2\Images\Heads\OpenSplash.png') -Force
Write-Host "Icon generated: $icoPath"

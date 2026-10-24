# SPDX-License-Identifier: MIT
param([string]$Version = '0.1.2')
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$packageRoot = Join-Path $PSScriptRoot "dist\PCLO-$Version"
if (!(Test-Path "$packageRoot\PCLO.exe")) { throw 'Run BUILD-PREVIEW.ps1 first.' }
$packageFiles = @('PCLO.exe','PCLO.exe.config','LICENCE','LICENSE-PCLO-MIT','LICENSING.md','README.md','PREVIEW-README.md','VALIDATION.md') |
    ForEach-Object { Join-Path $packageRoot $_ }
Compress-Archive -LiteralPath $packageFiles -DestinationPath "dist\PCLO-$Version-Windows.zip" -Force
$sourceRoot = Join-Path $PSScriptRoot ".build\source-$Version-$([Guid]::NewGuid().ToString('N'))\PCLO-$Version-source"
New-Item -ItemType Directory -Force $sourceRoot | Out-Null
$sourceFiles = @(git ls-files) + @('BUILD-PREVIEW.ps1','PACK-PREVIEW.ps1','BUILD-ICON.ps1','LICENSE-PCLO-MIT','LICENSING.md','PREVIEW-README.md','VALIDATION.md')
foreach ($relative in $sourceFiles) {
    $sourcePath = Join-Path $PSScriptRoot $relative
    if (Test-Path -LiteralPath $sourcePath -PathType Leaf) {
        $targetPath = Join-Path $sourceRoot $relative
        New-Item -ItemType Directory -Force (Split-Path $targetPath) | Out-Null
        Copy-Item -LiteralPath $sourcePath -Destination $targetPath -Force
    }
}
foreach ($relative in @(git -C MeloongCore ls-files)) {
    $sourcePath = Join-Path "$PSScriptRoot\MeloongCore" $relative
    if (Test-Path -LiteralPath $sourcePath -PathType Leaf) {
        $targetPath = Join-Path "$sourceRoot\MeloongCore" $relative
        New-Item -ItemType Directory -Force (Split-Path $targetPath) | Out-Null
        Copy-Item -LiteralPath $sourcePath -Destination $targetPath -Force
    }
}
Compress-Archive -LiteralPath $sourceRoot -DestinationPath "dist\PCLO-$Version-source.zip" -Force
Get-ChildItem "dist\PCLO-$Version-*.zip" | Select-Object Name,Length

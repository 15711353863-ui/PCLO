# SPDX-License-Identifier: MIT
param([string]$Version = '0.3.3-preview.2')
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$packageRoot = Join-Path $PSScriptRoot "dist\Plain-Craft-Launcher-Open-$Version"
if (!(Test-Path "$packageRoot\Plain Craft Launcher Open.exe")) { throw 'Run BUILD-PREVIEW.ps1 first.' }
$packageFiles = @('Plain Craft Launcher Open.exe','Plain Craft Launcher Open.exe.config','LICENCE','LICENSE-PCLO-MIT','LICENSING.md','NETWORK-DESIGN.md','OPEN-FEATURES.md','THIRD-PARTY-NOTICES.md','licenses','README.md','PREVIEW-README.md','VALIDATION.md') |
    ForEach-Object { Join-Path $packageRoot $_ }
Compress-Archive -LiteralPath $packageFiles -DestinationPath "dist\Plain-Craft-Launcher-Open-$Version-Windows.zip" -Force
$sourceRoot = Join-Path $PSScriptRoot ".build\source-$Version-$([Guid]::NewGuid().ToString('N'))\Plain-Craft-Launcher-Open-$Version-source"
New-Item -ItemType Directory -Force $sourceRoot | Out-Null
$sourceFiles = @(git -c core.quotepath=false ls-files --cached --others --exclude-standard) | Sort-Object -Unique
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
Compress-Archive -LiteralPath $sourceRoot -DestinationPath "dist\Plain-Craft-Launcher-Open-$Version-source.zip" -Force
Get-ChildItem "dist\Plain-Craft-Launcher-Open-$Version-*.zip" | Select-Object Name,Length

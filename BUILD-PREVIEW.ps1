# SPDX-License-Identifier: MIT
param(
    [string]$DotnetRoot = 'D:\Other\Codex\.tools\dotnet',
    [string]$Configuration = 'Release'
)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$env:DOTNET_ROOT = $DotnetRoot
$env:Path = "$DotnetRoot;$env:Path"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$sdkVersion = (& "$DotnetRoot\dotnet.exe" --version).Trim()
$env:MSBuildSDKsPath = "$DotnetRoot\sdk\$sdkVersion\Sdks"
$env:MSBuildEnableWorkloadResolver = 'false'
& "$DotnetRoot\dotnet.exe" restore 'PCLCS\PCLCS.csproj'
if ($LASTEXITCODE -ne 0) { throw 'NuGet restore failed.' }
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$msbuild = & $vswhere -latest -products '*' -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (!$msbuild) { throw 'Visual Studio MSBuild is required.' }
$referenceRoot = Join-Path $env:USERPROFILE '.nuget\packages\microsoft.netframework.referenceassemblies.net48\1.0.3\build'
& $msbuild 'Plain Craft Launcher 2\Plain Craft Launcher 2.vbproj' "/p:Configuration=$Configuration" "/p:TargetFrameworkRootPath=$referenceRoot\" /v:minimal /nologo
if ($LASTEXITCODE -ne 0) { throw 'PCL build failed.' }
$output = Join-Path $PSScriptRoot 'dist\PCLO-0.1.2'
New-Item -ItemType Directory -Force $output | Out-Null
Copy-Item 'Plain Craft Launcher 2\bin\Plain Craft Launcher 2.exe' "$output\PCLO.exe" -Force
Copy-Item 'Plain Craft Launcher 2\bin\Plain Craft Launcher 2.exe.config' "$output\PCLO.exe.config" -Force -ErrorAction SilentlyContinue
Copy-Item 'LICENCE','LICENSE-PCLO-MIT','LICENSING.md','README.md','PREVIEW-README.md','VALIDATION.md' $output -Force
Get-FileHash "$output\PCLO.exe" -Algorithm SHA256 | Format-List
Write-Host "Preview: $output"

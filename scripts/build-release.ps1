param([string]$Dotnet = 'dotnet')
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$version = '0.3.0'
& (Join-Path $PSScriptRoot 'build-desktop.ps1') -Dotnet $Dotnet
$release = Join-Path $repoRoot 'output\releases'
New-Item -ItemType Directory -Path $release -Force | Out-Null
$zip = Join-Path $release "AssetRaider-$version-Windows-x64.zip"
if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory((Join-Path $repoRoot "output\desktop-$version"), $zip)
$setup = Join-Path $repoRoot 'output\setup'
& $Dotnet publish (Join-Path $repoRoot 'installer\AssetRaider.Setup.csproj') -c Release -r win-x64 --self-contained true -p:DebugType=None -p:DebugSymbols=false -o $setup
if ($LASTEXITCODE -ne 0) { throw 'Installer publish failed.' }
Copy-Item -LiteralPath (Join-Path $setup 'AssetRaider.Setup.exe') -Destination (Join-Path $release "AssetRaider-$version-Setup.exe") -Force
$sums = Get-ChildItem -LiteralPath $release -File | Where-Object Extension -In '.exe', '.zip' | ForEach-Object { "{0}  {1}" -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $_.Name }
$sums | Set-Content -LiteralPath (Join-Path $release 'SHA256SUMS.txt') -Encoding ascii
Write-Host "Release files: $release"

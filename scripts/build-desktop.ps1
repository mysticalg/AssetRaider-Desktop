param([string]$Dotnet = 'dotnet', [string]$OutputDirectory = '')
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$project = Join-Path $repoRoot 'desktop\AssetRaider.Desktop.csproj'
$version = ([xml](Get-Content -LiteralPath $project -Raw)).Project.PropertyGroup.Version
$destination = if ($OutputDirectory) { [IO.Path]::GetFullPath($OutputDirectory) } else { Join-Path $repoRoot "output\desktop-$version" }
& $Dotnet publish $project -c Release -r win-x64 --self-contained true -p:DebugType=None -p:DebugSymbols=false -o $destination
if ($LASTEXITCODE -ne 0) { throw 'Desktop publish failed.' }
Copy-Item -LiteralPath (Join-Path $repoRoot 'desktop\README.md') -Destination (Join-Path $destination 'README.md')
Copy-Item -LiteralPath (Join-Path $repoRoot 'desktop\licenses') -Destination $destination -Recurse -Force
Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE') -Destination $destination -Force
Write-Host "Ready: $destination\AssetRaider.exe"

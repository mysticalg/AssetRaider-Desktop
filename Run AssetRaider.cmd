@echo off
if not exist "%~dp0output\desktop-0.3.1\AssetRaider.exe" (
  echo Build the desktop app first using scripts\build-desktop.ps1 with the .NET 10 SDK.
  pause
  exit /b 1
)
start "" "%~dp0output\desktop-0.3.1\AssetRaider.exe"

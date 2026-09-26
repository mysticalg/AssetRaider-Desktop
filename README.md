# AssetRaider Desktop

**Udio + Suno playback to WAV, on your Windows PC.** Choose tracks, select all, and record a queue into separate local audio files.

[Download website](https://mysticalg.github.io/AssetRaider-Desktop/) · [Windows downloads](https://github.com/mysticalg/AssetRaider-Desktop/releases/tag/v0.3.0-beta.1) · [Report a problem](https://github.com/mysticalg/AssetRaider-Desktop/issues)

## Install

Requires **Windows 11 x64 and Google Chrome**. The .NET runtime is included.

- **Installer:** download `AssetRaider-0.3.0-Setup.exe` from Releases, run it, then open AssetRaider from the Start menu. Installs for your Windows account without administrator access.
- **Portable:** download `AssetRaider-0.3.0-Windows-x64.zip`, extract the entire ZIP and run `AssetRaider.exe`. Keep all extracted files together, including `.playwright`.

This is an **unsigned public beta**. Windows may display an unknown-publisher warning. Review the source or build it yourself if you prefer. SHA-256 checksums accompany each release.

## Use

1. Choose **Udio** or **Suno**. Click **Sign in (normal Chrome)** and sign in in the separate window. Close that window when finished.
2. Click **Load library**. For Suno, this scans the selected Create workspace; filters affect the loaded list.
3. Choose tracks or **Select all**, choose a save folder, then **Record selected as WAV**.

Recordings are 48 kHz, 16-bit stereo PCM WAV, about 11 MB per minute. Recording happens at normal playback speed. Keep the dedicated Chrome window open and unmuted; it can be behind other windows. Finished recordings are tracked locally so a queue can be resumed.

These WAVs contain playback audio. They are not original studio masters or native service WAV exports; conversion does not improve source quality. Use with audio you have permission to record. AssetRaider is independent of Udio and Suno.

## Beta status

Version 0.3.0 adds Suno support and fixes the Udio `Return type mismatch ... ScrollState` error by explicitly converting structured browser results. Automated player/library tests, app launch/data checks and installer extraction/removal checks pass. The native Windows audio capture self-test has also been exercised locally. **A complete signed-in recording queue on both services has not yet been verified.** Site changes, login restrictions or playback conditions may require fixes.

Please include your app version, Windows version, chosen site and the error text in issue reports. Do not upload passwords, cookies, browser profile folders or private recordings.

## Privacy and uninstall

The app uses separate local Chrome profiles. You sign in directly on each service; it does not import your usual browser profile. There is no AssetRaider server, account, analytics or upload step. Chrome still connects to Udio/Suno and their sign-in providers normally. Recording mode uses a loopback-only local browser control connection. The microphone is not used.

Uninstall through Windows **Installed apps → AssetRaider Desktop**. Recordings and sign-in profiles are retained. The installer helper is retained in `%LOCALAPPDATA%\AssetRaiderInstaller`; after uninstall finishes, that folder may be removed manually. Portable copies can be removed by deleting their extracted app folder. Full usage details: [desktop/README.md](desktop/README.md).

## Build and test

Use Windows, the .NET 10 SDK, and Node.js 24 for JavaScript tests:

```powershell
npm ci
npm test
./scripts/build-release.ps1
$check = Start-Process ./output/releases/AssetRaider-0.3.0-Setup.exe -ArgumentList '--verify-install', 'C:\Temp\assetraider-installer.json' -Wait -PassThru
Get-Content C:\Temp\assetraider-installer.json
```

Release artifacts and SHA-256 checksums are written to `output/releases`. No signing certificate is configured. `--verify-install` extracts into a temporary owned folder, checks the bundled app and removes that extraction; it does not change your installed application. See the desktop README for the optional real audio self-test.

## License

MIT — see [LICENSE](LICENSE). Third-party notices are in [desktop/licenses](desktop/licenses) and the packaged `.playwright` directory.

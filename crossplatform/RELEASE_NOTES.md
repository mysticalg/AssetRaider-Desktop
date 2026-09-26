# AssetRaider 0.4.0 — macOS and Linux beta

AssetRaider now has desktop builds for **Apple Silicon Macs, Intel Macs, and Linux x86-64**, sharing the Udio/Suno library scanning, track selection, Select all and WAV queue with the Windows edition.

| Download | Requirements |
| --- | --- |
| `AssetRaider-0.4.0-osx-arm64.zip` | Apple Silicon, macOS 14+, Google Chrome |
| `AssetRaider-0.4.0-osx-x64.zip` | Intel Mac, macOS 14+, Google Chrome |
| `AssetRaider-0.4.0-linux-x64.tar.gz` | Ubuntu 22.04+ or comparable x86-64 desktop, native Google Chrome, PulseAudio/PipeWire-Pulse and pulseaudio-utils |

**Mac:** extract the ZIP, move AssetRaider.app to Applications. Grant Screen & System Audio Recording permission when requested. The helper requests audio only from the dedicated Chrome application, not microphone audio or saved screen video. These beta builds are ad-hoc signed, **not Apple Developer ID signed or notarized**; macOS may block opening downloaded apps.

**Linux:** extract the archive and run `AssetRaider/AssetRaider`, or run `bash AssetRaider/install.sh` for an applications-menu entry. The app creates a separate silent recording channel; other desktop audio is not recorded. No .NET installation required.

**Workflow:** choose Udio or Suno → Sign in (normal Chrome) → close that sign-in window → Load library → select tracks → Record selected as WAV. Keep the dedicated Chrome window open and the computer awake.

**Validation:** native builds and app/data/Playwright-driver startup checks on all three targets; real three-second Linux audio capture and WAV validation; Linux interface rendering. macOS's interactive permission/capture flow and full signed-in service queues remain unverified. This is an experimental beta, not a guarantee of uninterrupted service compatibility.

WAV output is 48 kHz / 16-bit stereo, at normal playback speed. It preserves playback quality, not an original master. Use audio you have permission to record. SHA256SUMS.txt covers all downloads.

[Full installation guide](https://github.com/mysticalg/AssetRaider-Desktop/blob/main/crossplatform/README.md) · [Windows 0.3.0 downloads](https://github.com/mysticalg/AssetRaider-Desktop/releases/tag/v0.3.0-beta.1) · [Report an issue](https://github.com/mysticalg/AssetRaider-Desktop/issues)

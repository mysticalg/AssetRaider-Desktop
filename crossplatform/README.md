# AssetRaider 0.4.1 beta — macOS and Linux

**Record again (new copy)** makes fresh recordings of selected tracks even if earlier WAVs are remembered. Existing files stay intact. Normal recording skips verified completed files and shows their full paths; the summary counts new recordings and skipped files separately. Songs are recognised by site and ID, even after renaming.

Choose Udio or Suno, sign in using normal Chrome, close the sign-in window, load the current library/workspace, select tracks (or Select all), and record separate local WAV files.

## macOS

- macOS **14 Sonoma or newer**, with Google Chrome installed in `/Applications` or your user's `Applications` folder.
- Download `osx-arm64` for Apple Silicon (M-series) or `osx-x64` for Intel. Extract the ZIP and drag **AssetRaider.app** to Applications.
- On first recording, allow **Screen & System Audio Recording** for AssetRaider in **System Settings → Privacy & Security**, then restart the app if macOS requests it. The helper uses ScreenCaptureKit and filters to the dedicated Chrome application PID. It requests audio output only; it does not save screen video or microphone audio.
- These beta apps are **not Developer ID signed or Apple notarized**. Gatekeeper may prevent opening a downloaded build. Developer builds and source are provided; no signing identity is configured. Do not disable system-wide security settings.
- To uninstall, remove AssetRaider.app from Applications. Local profiles and recordings remain.

## Linux

- **x86-64**, Ubuntu 22.04/24.04 or a comparable glibc desktop with X11/XWayland, ICU, libX11, libICE, libSM, fontconfig, libGL, and Google Chrome's native `.deb`/`.rpm` package. Snap/Flatpak Chrome profiles are not supported.
- Requires a running **PulseAudio or PipeWire-Pulse** sound server, plus `pactl` and `parec`. On Debian/Ubuntu install `pulseaudio-utils`; on Fedora install `pulseaudio-utils`. The app does not replace or reconfigure your default sound server.
- Extract `AssetRaider-0.4.1-linux-x64.tar.gz`. Run `AssetRaider/AssetRaider`, or run `bash AssetRaider/install.sh` for a per-user installation and applications-menu entry. No .NET installation or root access is required by AssetRaider itself.
- Recording mode creates a dedicated virtual sink and routes only its Chrome process to it. **That browser's playback is intentionally silent on Linux** while it records. Your microphone and normal desktop output are not captured.
- Use `~/.local/share/AssetRaiderApp/uninstall.sh` to remove an installed copy. Recordings and browser sign-ins are preserved. A portable copy can be removed by deleting its extracted app folder.

## Shared behavior and limitations

48 kHz / 16-bit stereo WAV, real-time playback, about 11 MB per minute. Keep the dedicated Chrome window open and the computer awake. These are playback recordings, not recovered original masters. A stalled, silent or failed capture remains partial. Completed recordings and the local ledger can be used to resume a queue.

macOS/Linux editions reuse the Windows library-scanning and playback controller. Full signed-in queues require manual service testing. GitHub's CI checks the native platform builds, app/data/driver startup, and Linux real PulseAudio capture. macOS recording permission and capture still require an interactive Mac test. An unattended build test cannot prove that permission or site playback works.

Sign-in profiles and settings live in the operating system's local application-data directory under `AssetRaider` (normally `~/.local/share/AssetRaider` on Linux). They are separate from your normal Chrome profile. No AssetRaider account, analytics or upload service is used.

## Build

Install .NET 10 SDK, and Xcode command-line tools for macOS. From the repository root:

```sh
bash scripts/build-unix.sh osx-arm64  # on an Apple Silicon Mac
bash scripts/build-unix.sh osx-x64    # on an Intel Mac
bash scripts/build-unix.sh linux-x64  # on Linux x86-64
```

`AssetRaider --launch-check <folder>` validates shared data handling and starts the bundled Playwright driver without opening Chrome. `AssetRaider --audio-check <folder>` on Linux records a three-second test tone through its dedicated sink and checks audible PCM WAV output. It never uses a microphone. The macOS helper's `--check` only checks that the helper can launch and link its frameworks; it does not capture audio.

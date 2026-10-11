# AssetRaider 0.4.8 beta — macOS and Linux

## Music player

The selection-column header toggles all visible tracks. Click **Song**, **Length** or **Status** to sort, then click again to reverse. Selections and the name filter are preserved; duration sorting is numeric. Track artwork loads as rows become visible.

**Next** and automatic advance follow the visible sorted list. **Shuffle** plays each visible track once per cycle. **Repeat** cycles through **Off**, **All** (repeat the list) and **One** (loop the current track). WAV recording retains its normal queue behavior.

Click **Play** beside a scanned track to listen without saving a file. Pause, resume, replay and seek from the player bar while the dedicated Chrome window stays minimized. The spectrum shows actual captured audio, also during recording. Scanning, changing sites and recording stop preview playback first. Linux connects its private audio channel to your default speakers only while previewing. macOS needs its existing system-audio permission for the spectrum.

Pressing **Play** during a running or paused scan stops scanning and plays the chosen track automatically, keeping the tracks already found. **Stopping scan…** and **Buffering…** show the transition while any browser step already in progress finishes. Playback continues to be blocked during recording.

## Source quality

Each playing or recording track shows the codec, approximate stream bitrate (`~kbps`), sample rate (`kHz`) and channels reported by Chrome for its selected player. Detection starts during preparation/playback, not scanning. Unavailable fields, including bitrate, are omitted; no detected fields shows **Source quality unavailable**. Bitrate is a stream average and may include headers/container overhead; it is withheld for video/multiple-audio resources. These figures describe the playback input, independently of the 48 kHz / 16-bit stereo WAV output.

Detected values are saved with the completed-track history. Skipped older WAVs without stored source metadata show unknown; the app cannot infer their original encoding from the WAV. Detection does not download a second audio copy or retain stream URLs.


This update fixes Suno recordings failing with **A different player attempted to start** when animated cover art plays beside the song. The cover video is kept paused during capture, while the song audio remains the recording timer. Stopping preparation now cancels it promptly, and old cleanup cannot remove a retry's playback hook. Protection against a genuinely different audio track remains enabled.


## Library scanning and track-name search

- **Open library / filters** opens the dedicated Chrome library window. Suno opens `https://suno.com/me`; an already-open `/me` or `/create` view keeps its filters and selected workspace. Choose the Songs tab and any site filters before scanning.
- **Scan library** starts a fresh scan of that view and replaces the previous scanned list, including when the new view has no results.
- **Track name** filters scanned titles as you type, ignoring case. Matches update as new tracks arrive. Clear the text to see all scanned tracks. **Select all** and recording apply only to the currently visible matches; hidden selections are not recorded.
- **Find in library** (or Enter) starts a fresh scan. On Suno it applies the entered text to the site's library search, so older matches can be found directly without first scanning every song. Other Suno filters still apply. For Udio it scans the current view and filters the results locally. Empty text clears Suno's search when you press Find in library.
- **Pause scan / Resume scan** keeps the same scan, deduplication list and browser position while the app remains open. Pausing takes effect after the current browser step, and paused time does not use the scan's ten-minute active-time limit.
- **Clear scanned list** stops any scan, waits for its current step, then clears the app's results. It never deletes WAVs or completion history. **Stop** ends the scan but keeps tracks found. To change the site's filters mid-scan, Stop or Clear first, change them in Chrome, then start a fresh scan.


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
- Extract `AssetRaider-0.4.3-linux-x64.tar.gz`. Run `AssetRaider/AssetRaider`, or run `bash AssetRaider/install.sh` for a per-user installation and applications-menu entry. No .NET installation or root access is required by AssetRaider itself.
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

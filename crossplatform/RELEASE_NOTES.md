# AssetRaider 0.4.9 — library views and full prompts

Fitted artwork with adjustable sizes, List/Grid views, creation/release-date and model columns, and full prompt viewing/copying. Missing release dates remain unavailable. These controls share the Windows library metadata parser and preserve recording selection across view changes.

Selection-column header toggle, ascending/descending Song/Length/Status sorting, artwork thumbnails, Next, automatic advance, Shuffle and Repeat Off/All/One. Playback follows the visible sorted list; numeric durations and missing lengths sort correctly. Playing a track stops an active or paused scan and keeps tracks found.

Play scanned tracks directly from the app, with pause/resume, replay, seeking and elapsed time. Chrome can remain minimized. The spectrum displays the dedicated browser's actual audio activity, including during recording. Listening writes no audio files. Scanning, changing sites and recording stop preview playback first. Unknown bitrate is omitted. Linux preview audio is routed from the private recording channel to speakers while listening.

The new player and spectrum compile and pass interface checks; live playback was tested on Windows. Native macOS/Linux playback and permission handling for this update still need runtime verification.

## Previous release: 0.4.6

**New in 0.4.6:** optional media-diagnostics cleanup runs outside the recording stop path, so a slow connection cannot append silence to a completed capture. Includes per-track source quality: codec, approximate browser-reported stream bitrate, sample rate and channels. Values update during recording and are saved in the completed-track history. Missing/ambiguous metadata stays unknown; video or multiple-audio-track resources do not receive an audio bitrate estimate. The generated app icon and refreshed layout remain included.

Bitrate may include headers/container overhead and is labelled `~`. This is the playback source quality, separate from the unchanged 48 kHz / 16-bit WAV output. Detection begins with recording; the library scan does not play every track. Older saved tracks without source metadata show unknown. No stream URLs are stored.

**Playback fix included:** fixes Suno animated cover video causing a false different-player error. Video is paused during capture, audio remains the timer, and stop/retry cleanup cannot break a newer attempt. Different audio tracks still trigger protection.

**Library controls included:** Suno `/me` library, Open library / filters, instant track-name filtering, direct Suno library search, Pause/Resume scan, Clear scanned list and updated library pagination. Scan cancellation and search regression checks cover both desktop implementations.

**Also included:** click **Record again (new copy)** to re-record selected tracks even when older WAVs are remembered. Existing files are preserved. Skips show the actual saved path, and summaries distinguish new recordings from skipped files. Shared queue regression checks cover both Udio and Suno, including renamed songs, missing/damaged files, failed retries and cancellation.

AssetRaider now has desktop builds for **Apple Silicon Macs, Intel Macs, and Linux x86-64**, sharing the Udio/Suno library scanning, track selection, Select all and WAV queue with the Windows edition.

| Download | Requirements |
| --- | --- |
| `AssetRaider-0.4.6-osx-arm64.zip` | Apple Silicon, macOS 14+, Google Chrome |
| `AssetRaider-0.4.6-osx-x64.zip` | Intel Mac, macOS 14+, Google Chrome |
| `AssetRaider-0.4.6-linux-x64.tar.gz` | Ubuntu 22.04+ or comparable x86-64 desktop, native Google Chrome, PulseAudio/PipeWire-Pulse and pulseaudio-utils |

**Mac:** extract the ZIP, move AssetRaider.app to Applications. Grant Screen & System Audio Recording permission when requested. The helper requests audio only from the dedicated Chrome application, not microphone audio or saved screen video. These beta builds are ad-hoc signed, **not Apple Developer ID signed or notarized**; macOS may block opening downloaded apps.

**Linux:** extract the archive and run `AssetRaider/AssetRaider`, or run `bash AssetRaider/install.sh` for an applications-menu entry. The app creates a separate silent recording channel; other desktop audio is not recorded. No .NET installation required.

**Workflow:** choose Udio or Suno → Sign in (normal Chrome) → close that sign-in window → Scan library → select tracks → Record selected as WAV. Keep the dedicated Chrome window open and the computer awake.

**Validation:** native builds and app/data/Playwright-driver startup checks on all three targets; real three-second Linux audio capture and WAV validation; Linux interface rendering. macOS's interactive permission/capture flow and full signed-in service queues remain unverified. This is an experimental beta, not a guarantee of uninterrupted service compatibility.

WAV output is 48 kHz / 16-bit stereo, at normal playback speed. It preserves playback quality, not an original master. Use audio you have permission to record. SHA256SUMS.txt covers all downloads.

[Full installation guide](https://github.com/mysticalg/AssetRaider-Desktop/blob/main/crossplatform/README.md) · [Windows 0.3.5 downloads](https://github.com/mysticalg/AssetRaider-Desktop/releases/tag/v0.3.5-beta.1) · [Report an issue](https://github.com/mysticalg/AssetRaider-Desktop/issues)

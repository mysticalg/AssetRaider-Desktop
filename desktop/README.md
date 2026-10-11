# AssetRaider desktop — Udio + Suno player and WAV recorder (0.3.7)

## Music player

Click **Play** beside a scanned track to listen without saving a file. Use the row button or player bar to pause, resume or replay; drag the seek slider to change position. The dedicated Chrome window can stay minimized. The live spectrum shows the browser's actual audio during listening and recording; it does not change the sound. Starting a scan, changing sites or recording stops the preview first. Playback controls are disabled during recording.

Pressing **Play** during a running or paused scan stops scanning and plays the chosen track automatically. Tracks already found stay in the list. The app finishes any browser step already in progress before loading the song; **Stopping scan…** and **Buffering…** show that transition.

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


Run `AssetRaider.exe` from the portable output folder, or `Run AssetRaider.cmd` in the repository. Keep all files in the output folder together, including the hidden `.playwright` directory. No extension or .NET installation is needed for the published Windows x64 build. Google Chrome and Windows 11 are required (the underlying process capture API needs Windows build 20348+). Version 0.3.3 also fixes the “Return type mismatch … ScrollState” library-loading error and applies the same conversion fix to track lists and playback progress.

1. Choose **Udio** or **Suno** in the **Music site** selector. Click **Sign in (normal Chrome)**. Sign in normally in the separate Chrome window, with no browser automation connected. Once your songs appear, **close that separate Chrome window** so Chrome saves your login. The app does not import your usual browser profile or ask for your password. Google can reject sign-in in an automated browser, so authentication has its own ordinary Chrome step.
2. Click **Scan library**. The app reopens that site's local profile for recording and scrolls through the current library view. For Suno, it scans the selected library view or Create workspace, including previous/next pages; it does not switch workspaces automatically. Udio filters and folders and Suno workspace filters affect which tracks appear. Select a different workspace in the app's Chrome window and Scan library again to scan it. A stopped scan keeps the tracks already found. On subsequent launches, go straight to Scan library if your login is still valid.
3. Tick songs, or click **Select all**. Pick an output folder and click **Record selected as WAV**. To make fresh copies even if the app remembers earlier recordings, click **Record again (new copy)**. Both actions keep existing WAVs; repeat filenames get a number when needed.

When a track is skipped, its status and log show the existing WAV's full path. Tracks are recognised by site and song ID, so renaming a song on Udio or Suno still matches the older file. The queue summary counts newly recorded and skipped tracks separately. Missing or damaged saved files are retried normally.

The queue opens each exact song, rewinds it, records its normal playback and saves a separate 48 kHz / 16-bit stereo PCM WAV. Recording takes the song's playback duration and about 11 MB per minute. Both sites use playback recording; these files are not Suno's native WAV exports. This records the streaming playback quality; it does not recover the original uncompressed master or improve its quality. **Select all** selects the loaded list for the chosen site/workspace. Changing sites clears the visible list and its selection.

Keep the dedicated Chrome window open. It can sit behind other windows. Leave its audio unmuted and avoid other playback in that dedicated window. The capture includes that Chrome process and its children; your usual browser and other applications are outside its capture scope. The microphone is never used. Login, browser audio settings, network stalls, site changes or playback protection can still prevent successful recording; a failed or silent recording is not marked complete. Suno's short silent keep-alive player is excluded from track timing.

**Stop** pauses playback and keeps an unfinished `.partial.wav`. Finished WAVs are recorded in `assetraider-completed.json` alongside the files. Keep this list to resume without recording completed songs again. Existing files are never overwritten. An app or system crash may leave a partial WAV with an unfinished header; it is never treated as completed. A selected track that fails is left with its error status and the queue continues.

Closing the app stops the current queue and closes its recording Chrome instance. A manual sign-in window is left under your control. Sign-ins remain locally in `%LOCALAPPDATA%\AssetRaider\ChromeProfile` (existing Udio profile) and `ChromeProfile-Suno` (Suno). Local Chrome debugging is enabled only in recording mode, bound to loopback for these dedicated profiles. No debugging or automation connection is used during manual authentication. Completed-track IDs include the site, and old Udio completion lists remain supported.

## Build and verify

With the .NET 10 SDK installed:

```powershell
./scripts/build-desktop.ps1
$check = Start-Process ./output/desktop-0.3.7/AssetRaider.exe -ArgumentList '--self-test', 'C:\Temp\AssetRaiderTest' -Wait -PassThru
Get-Content C:\Temp\AssetRaiderTest\self-test.json
```

The self-test plays a quiet three-second tone and captures this process's real Windows audio output. It checks audible WAV data, duration, stable names, resume, and partial-file rejection. `--launch-check C:\Temp\AssetRaiderCheck` checks manual login arguments, structured browser data conversion, and migration of completed tracks without playing sound. Neither check verifies a signed-in site recording.

Dependencies: [NAudio](https://github.com/naudio/NAudio) (MIT), [Microsoft Playwright .NET](https://github.com/microsoft/playwright-dotnet) (MIT, with the Apache-2.0 Playwright driver), Microsoft .NET runtime. Licenses are included in `licenses` and `.playwright`. NAudio's [process-loopback documentation](https://github.com/naudio/NAudio/blob/main/Docs/WasapiRecorder.md) describes the Windows recording API used here. The app does not intercept encrypted media segments or license keys.

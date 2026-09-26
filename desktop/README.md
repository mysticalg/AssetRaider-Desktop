# AssetRaider desktop — Udio + Suno WAV recorder (0.3.1)

Run `AssetRaider.exe` from the portable output folder, or `Run AssetRaider.cmd` in the repository. Keep all files in the output folder together, including the hidden `.playwright` directory. No extension or .NET installation is needed for the published Windows x64 build. Google Chrome and Windows 11 are required (the underlying process capture API needs Windows build 20348+). Version 0.3.1 also fixes the “Return type mismatch … ScrollState” library-loading error and applies the same conversion fix to track lists and playback progress.

1. Choose **Udio** or **Suno** in the **Music site** selector. Click **Sign in (normal Chrome)**. Sign in normally in the separate Chrome window, with no browser automation connected. Once your songs appear, **close that separate Chrome window** so Chrome saves your login. The app does not import your usual browser profile or ask for your password. Google can reject sign-in in an automated browser, so authentication has its own ordinary Chrome step.
2. Click **Load library**. The app reopens that site's local profile for recording and scrolls through the current library view. For Suno, it scans the selected Create workspace, including previous/next pages; it does not switch workspaces automatically. Udio filters and folders and Suno workspace filters affect which tracks appear. Select a different workspace in the app's Chrome window and Load library again to scan it. A stopped scan keeps the tracks already found. On subsequent launches, go straight to Load library if your login is still valid.
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
$check = Start-Process ./output/desktop-0.3.1/AssetRaider.exe -ArgumentList '--self-test', 'C:\Temp\AssetRaiderTest' -Wait -PassThru
Get-Content C:\Temp\AssetRaiderTest\self-test.json
```

The self-test plays a quiet three-second tone and captures this process's real Windows audio output. It checks audible WAV data, duration, stable names, resume, and partial-file rejection. `--launch-check C:\Temp\AssetRaiderCheck` checks manual login arguments, structured browser data conversion, and migration of completed tracks without playing sound. Neither check verifies a signed-in site recording.

Dependencies: [NAudio](https://github.com/naudio/NAudio) (MIT), [Microsoft Playwright .NET](https://github.com/microsoft/playwright-dotnet) (MIT, with the Apache-2.0 Playwright driver), Microsoft .NET runtime. Licenses are included in `licenses` and `.playwright`. NAudio's [process-loopback documentation](https://github.com/naudio/NAudio/blob/main/Docs/WasapiRecorder.md) describes the Windows recording API used here. The app does not intercept encrypted media segments or license keys.

# AssetRaider Desktop 0.3.0 — public beta

Choose Udio or Suno tracks and record normal playback into separate local WAV files.

- Windows installer with Start menu shortcut and Installed apps entry, plus a portable ZIP.
- Track selection and Select all for the loaded library/workspace.
- Stereo 48 kHz / 16-bit WAV recordings, completion tracking and resumable queues.
- Fixes the Udio `Return type mismatch ... ScrollState` library-loading error.
- Adds Suno workspace scanning and playback support.

**Requirements:** Windows 11 x64, installed Google Chrome, and access to playable tracks on the chosen service. The .NET runtime is bundled. Both downloads are unsigned.

**Install:** run the Setup EXE; or extract the full portable ZIP and run AssetRaider.exe. Choose a site, sign in using the normal Chrome button, close the sign-in window, then load the library and record selected tracks.

**Validation:** automated player/library tests, app launch/data checks, and installer payload extraction/removal pass. Native Windows process audio capture has been exercised locally. Complete signed-in recording queues on both services remain unverified; this is a beta release. Playback recording takes real time and does not recover or improve the original master.

SHA256SUMS.txt lists hashes for both downloads. Report issues with the app version, chosen site and error message. Never attach credentials or browser profiles.

# AssetRaider Desktop 0.3.4 — interface and icon refresh

A clearer library-to-recording layout, a new waveform app icon, and a matching green Record button. Library sign-in, scan controls and track search are grouped above the list; the save folder and recording controls sit below it. The layout gives tracks more space and keeps controls visible at the minimum window size.

The generated icon is included in the Windows app, installer and download website. Recording output remains 48 kHz / 16-bit stereo WAV; this refresh does not change the playback or capture engine and does not add source bitrate detection.

Includes the 0.3.3 fix for Suno animated covers triggering a false different-player error, plus stop/retry cleanup. Suno recording on Windows was reported working by the user on 11 October 2026. Library search, filters, pause/resume scanning, clearing scanned tracks and recording fresh copies remain available.

Validation: browser-script regression tests, Windows minimum-size UI rendering and clipping checks, app/data and installer checks. Mac/Linux packages have separate releases. Full signed-in queues across both services remain unverified.

Windows 11 x64 and Google Chrome required. Close the running app before installing or opening the update. Runtime included; builds remain unsigned. SHA256SUMS.txt covers both downloads.

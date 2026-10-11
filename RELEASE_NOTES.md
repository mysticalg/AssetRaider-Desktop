# AssetRaider Desktop 0.3.7 — music player and live spectrum

Play any scanned track from its row. The music player provides pause/resume, replay, seeking, elapsed time and source quality while the dedicated Chrome window is minimized. Starting a scan, changing sites or recording stops the preview first; playback and seeking controls are unavailable during recording.

The live spectrum uses the dedicated browser's PCM audio during listening and recording. Listening does not write WAV files. The graphic is for presentation and does not change the audio. Unavailable bitrate is omitted from source-quality text.

Validation: a live Suno track played with Chrome minimized; pause, seek, resume, completion, replay and stop passed. The spectrum responded to real browser audio, stopping reset it, and previewing created no WAV files. Recording capture, source-quality and minimum-size interface checks remain covered separately.

## Reliable playback startup (introduced in 0.3.6)

Fixes a Suno startup timeout when the song's Play button appears before its click handler is ready. Preparation now rechecks the current, enabled Play button while waiting for the audio player. It stops retrying as soon as the player is found, preserves the selected-song checks, and never clicks Pause. Cancellation and failures remove the retry timer.

Validation: 19 playback/scanning tests; real Chrome playback with a delayed, replaced Play button; Windows audio capture and installer checks. A live Suno cover track reproduced the old 20-second timeout and completed a 173.28-second audible stereo WAV using the patched recording queue. One earlier attempt paused and was retained as a partial file. Source quality detection remains available as described below.

## Source audio quality (introduced in 0.3.5)

The new **Source quality** column shows the codec, approximate stream bitrate, sample rate and channel count reported by Chrome for the exact audio player being recorded. It updates during playback and is saved in the completed-track history and terminal log. For example: `MP3 · ~97.9 kbps · 44.1 kHz · stereo`.

Bitrate is a browser-reported average, potentially including headers/container overhead, not an exact encoder setting or instantaneous VBR measurement. It is withheld for video or multiple-audio-track resources. Missing, ambiguous or unsupported metadata is shown as unknown; detection never guesses from the WAV format or another player. Suno's keep-alive audio and animated cover cannot be selected by proximity.

Quality is detected when preparing/recording a track, not during the library scan. Older completed recordings without stored source metadata show unknown when skipped. The WAV output remains 48 kHz / 16-bit stereo and does not improve compressed source quality. No stream URLs or browser diagnostics are stored in the history.

Validation: 17 browser-script tests; source matching, ambiguous/malformed metadata, mixed audio/video bitrate, changes and history regression checks; isolated real Chrome playback of a known 96 kbps / 44.1 kHz stereo MP3 through the actual player preparation script; minimum-size Windows UI and installer/data checks. Native Mac/Linux packages have separate releases. Detection availability on each live service depends on what Chrome exposes.

Windows 11 x64 and Google Chrome required. Close the running app before installing or opening the update. Runtime included; builds remain unsigned. SHA256SUMS.txt covers both downloads.

# AssetRaider Desktop 0.3.5 — source audio quality

The new **Source quality** column shows the codec, approximate stream bitrate, sample rate and channel count reported by Chrome for the exact audio player being recorded. It updates during playback and is saved in the completed-track history and terminal log. For example: `MP3 · ~97.9 kbps · 44.1 kHz · stereo`.

Bitrate is a browser-reported average, potentially including headers/container overhead, not an exact encoder setting or instantaneous VBR measurement. It is withheld for video or multiple-audio-track resources. Missing, ambiguous or unsupported metadata is shown as unknown; detection never guesses from the WAV format or another player. Suno's keep-alive audio and animated cover cannot be selected by proximity.

Quality is detected when preparing/recording a track, not during the library scan. Older completed recordings without stored source metadata show unknown when skipped. The WAV output remains 48 kHz / 16-bit stereo and does not improve compressed source quality. No stream URLs or browser diagnostics are stored in the history.

Validation: 17 browser-script tests; source matching, ambiguous/malformed metadata, mixed audio/video bitrate, changes and history regression checks; isolated real Chrome playback of a known 96 kbps / 44.1 kHz stereo MP3 through the actual player preparation script; minimum-size Windows UI and installer/data checks. Native Mac/Linux packages have separate releases. Detection availability on each live service depends on what Chrome exposes.

Windows 11 x64 and Google Chrome required. Close the running app before installing or opening the update. Runtime included; builds remain unsigned. SHA256SUMS.txt covers both downloads.

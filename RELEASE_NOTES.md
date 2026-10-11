# AssetRaider Desktop 0.3.3 — Suno animated-cover recording fix

Fixes **A different player attempted to start** when Suno starts an animated cover video alongside song audio. The controller pauses cover videos and keeps the actual song audio as the recording timer. A different audio track still triggers protection against recording the wrong song.

Also fixes stop/retry cleanup: pending preparation cancels promptly, cleanup is idempotent, and an older preparation cannot remove the playback hook of a newer attempt.

Library search, filters, pause/resume scanning and Record again (new copy) remain available. Existing WAVs and partial recordings are preserved.

Validation: 17 browser-script tests, including cover-video-before/after-audio, different-audio rejection and stop/retry races; live playback-controller check on the affected Suno song; app/data and installer checks. This fix does not change native audio capture. Complete WAV export of the affected song is not part of this validation.

User confirmation (11 October 2026): Suno recording on Windows was reported working after this update. This feedback is separate from the automated checks above; interactive Mac capture and complete queues across both services still need testing.

Windows 11 x64 and Google Chrome required. Close the running app before installing or opening the update. Runtime included; builds remain unsigned. SHA256SUMS.txt covers both downloads.

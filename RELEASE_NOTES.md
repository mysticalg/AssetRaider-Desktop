# AssetRaider Desktop 0.3.2 — library search and scan controls

- Suno opens the user's library at `https://suno.com/me`. **Open library / filters** lets you choose site filters before scanning.
- Type a track name to filter scanned results immediately. **Find in library** searches Suno directly and scans matching results; Udio scans the current view and applies the name filter locally.
- **Pause scan / Resume scan** retains your position. **Clear scanned list** safely stops scanning and clears results without deleting recordings.
- Fresh scans replace stale results. Select all and recording apply to visible matches only.
- Updated pagination for Suno's current library arrows. Existing repeat-recording and WAV preservation remain available.

Windows 11 x64 and Google Chrome required. The runtime is bundled; builds remain unsigned. Close the old app before installing the update.

Validation: name search and current pagination inspected in a signed-in Suno library; 13 browser-script tests; shared scan tests for pause/resume, page deduplication, cancellation and late-result rejection; Windows interface search/layout checks; app and installer checks. Full signed-in recording queues remain outside this update's validation.

SHA256SUMS.txt covers the installer and portable ZIP.

# AssetRaider 0.3.12 — Windows beta

Individual track checkboxes now work while a library scan is running or paused.
Select all and Select none are available during scans too. Selected tracks stay
selected as scan results refresh; recording still locks the selection and queue.
Track metadata remains read-only. The Suno Play-button repair from 0.3.11 is included.

Validation: real checkbox clicks selected and deselected tracks in paused scans,
selected a track in an active scan, and verified Select all / Select none. A
recording-state click could not alter selection. Native UI checks cover editable
checkboxes, metadata protection, scan refreshes, filtering/sorting and recording
locks. All 25 playback/library script tests and installer extraction checks pass.

## Included from 0.3.11

Suno playback now selects the labelled song Play action when its cover overlay
also exposes a Play button. This fixes preparation rejecting the current song
page as ambiguous, including narrow windows and hovered cover artwork. Genuine
ambiguity still fails safely; Udio selection and recording protections are unchanged.

Validation: all 25 playback/library tests passed. Live signed-in Suno checks
verified audible playback with the default EQ, pause/resume, seek, completion
and replay, plus audible five-second recording samples from two songs. Full
recording queues and live Udio playback were not revalidated. Settings, Chrome
profiles and existing recordings are preserved.

## Included from 0.3.10

- Eight-band playback EQ (63 Hz–8 kHz), ±12 dB band sliders and a separate ±12 dB Gain slider. Bypass and Flat reset are included. Boosts raise the level directly; reduce Gain if audio distorts.
- Responsive side-by-side equalizer and live audio visualizer. Right-click for Spectrum, Neon wave, Mirror, Orbit or Fire, and 15/24/30/45/60/90/120 FPS targets. Default: 60 FPS.
- EQ gains, preamp, bypass, visualizer style and target frame rate persist across restarts.
- Track name and Tag / genre search modes, plus a Tags / genres table column. Tags use scanned style metadata rather than lyrics or excluded styles.
- Suno name searches update after a 650 ms typing pause through the signed-in web search form. New typing cancels the previous scan. All returned Suno matches are retained, including matches whose titles do not contain the query. Pagination and scrolling remain supported within the existing ten-minute scan limit.

EQ affects listening audio; recordings retain the original source. Web Audio playback requires sources that permit CORS. Existing Chrome profiles, saved recordings, completion history and output-folder settings are preserved.

Validation includes playback/library tests, native UI and launch checks, an isolated Chrome real-PCM EQ/preamp test, source-quality checks and installer payload/extraction checks. Full signed-in queues on both services have not been revalidated for this release.

Windows 11 x64 and Google Chrome are required. The installer is per-user and unsigned. SHA-256 checksums are included.

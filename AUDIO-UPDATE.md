AssetRaider Windows 0.3.10 audio and search update

The audio panel divides evenly between the live visualizer and an eight-band playback equalizer. Frequencies: 63, 125, 250, 500 Hz; 1, 2, 4, 8 kHz. Each slider supports -12 to +12 dB. Uncheck 8-BAND EQ to bypass; Flat resets gains. The separate Gain slider controls the preamp from -12 to +12 dB. Band boosts raise the level directly; reduce Gain to avoid clipping. Bypass disables the gain and all bands.

Right-click the visualizer to select Spectrum, Neon wave, Mirror, Orbit or Fire, cycle to the next style, or select a target frame rate (15, 24, 30, 45, 60, 90, 120 FPS). The default is 60 FPS; achieved frame rate depends on display and system load. Controls resize with the window and use a compact layout on shorter windows.

EQ, preamp gain, style and frame rate save automatically in %LOCALAPPDATA%\AssetRaider\audio-settings.json. The existing destination setting and Chrome profiles are preserved. EQ filters listening audio in Chrome using Web Audio; WAV recordings use the original source audio. A source must permit Web Audio/CORS; if playback reports a CORS error, bypass EQ and retry.

Validation: 22 existing playback tests; native launch and UI checks; real Chrome 1 kHz PCM checks verified -12 dB attenuation, bypass, +12 dB band boosts, ±6 dB preamp gain, context cleanup and preference reload. Actual signed-in Udio/Suno library playback requires the user's account and was not exercised automatically.

Search update: choose Track name or Tag / genre. Tag / genre filters scanned style tags and has its own table column; Find in library scans Suno with its web search cleared to gather tags across the library. Track name uses Suno’s web form when Find in library is clicked, then automatically refreshes after a 650 ms typing pause using the signed-in Suno browser. All returned Suno results are kept, including matches whose titles do not contain the query. New typing cancels the older scan. Pagination and scroll scanning retain the existing ten-minute active-time limit.

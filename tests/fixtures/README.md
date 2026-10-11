# Source quality fixture

`source-96k-44100.mp3` is a generated four-second 440 Hz sine tone, MP3 at 96 kbps, 44,100 Hz, two channels. It contains no third-party recording.

Generated with:

```sh
ffmpeg -f lavfi -i 'sine=frequency=440:duration=4:sample_rate=44100' -ac 2 -c:a libmp3lame -b:a 96k source-96k-44100.mp3
```

The Chrome test deliberately plays a detached HTMLAudioElement from a blob URL, as Suno does. The measured stream average may include headers/container overhead (about 97.9 kbps for this short fixture), so the app labels it with `~`; it does not claim an exact encoder bitrate. The fixture must still report MP3, 44,100 Hz and stereo rather than the recorder's 48,000 Hz output format.

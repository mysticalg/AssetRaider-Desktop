using System.Diagnostics;
using System.Text.Json;
using Microsoft.Playwright;
using NAudio.Wave;

namespace AssetRaider.Desktop;

internal static class CrossCheck
{
    public static int Run(string directory)
    {
        Directory.CreateDirectory(directory);
        try
        {
            var profile = Path.Combine(directory, "Example Profile");
            var login = MusicBrowser.LaunchOptions("chrome", profile, false);
            var recording = MusicBrowser.LaunchOptions("chrome", profile, true);
            if (login.ArgumentList.Any(a => a.Contains("debugging") || a.Contains("automation"))) throw new Exception("Manual login enabled automation.");
            if (!recording.ArgumentList.Contains("--remote-debugging-address=127.0.0.1")) throw new Exception("Recording port is not restricted to loopback.");
            if (OperatingSystem.IsLinux() && recording.Environment["PULSE_SINK"] != PlatformAudio.Sink) throw new Exception("Chrome audio is not isolated.");
            var scroll = BrowserData.Decode<ScrollState>("{\"Bottom\":true,\"Position\":123}");
            if (!scroll.Bottom || scroll.Position != 123) throw new Exception("Structured browser results did not decode.");
            foreach (var name in new[] { "player", "library", "scroll", "pagination" }) if (MusicBrowser.Script(name).Length < 30) throw new Exception("Missing browser script: " + name);
            using (var driver = Playwright.CreateAsync().GetAwaiter().GetResult()) { }
            RecordingPolicyCheck.RunAsync(Path.Combine(directory, "repeat-" + Guid.NewGuid().ToString("N"))).GetAwaiter().GetResult();
            var track = new Track { Id = "ExampleTrack0001", Title = "Sample: song/unsafe?*" };
            var suno = new Track { Id = track.Id, Title = track.Title, Site = "suno" };
            var changes = 0; track.PropertyChanged += (_, _) => changes++; track.Selected = true; track.Status = "Testing";
            if (changes != 2) throw new Exception("Selection/status binding does not notify.");
            if (RecordingLibrary.SafeName(track).IndexOfAny("<>:\"/\\|?*".ToCharArray()) >= 0) throw new Exception("Filename is not portable.");
            var library = new RecordingLibrary(Path.Combine(directory, "files-" + Guid.NewGuid().ToString("N"))); library.Load();
            var paths = library.Allocate(track); WriteTone(paths.Partial, 1);
            if (library.IsComplete(track)) throw new Exception("Partial recording treated as complete.");
            library.Complete(track, paths.Partial, paths.Final, 1);
            var again = new RecordingLibrary(library.DirectoryPath); again.Load();
            if (!again.IsComplete(track) || again.IsComplete(suno) || again.Allocate(track).Final == paths.Final) throw new Exception("Resume/site isolation/non-overwrite validation failed.");
            try { RecordingLibrary.SafeName(new Track { Id = "../escape" }); throw new Exception("Traversal was accepted."); } catch (ArgumentException) { }
            File.WriteAllText(Path.Combine(directory, "launch-check.json"), JsonSerializer.Serialize(new { Passed = true, Platform = System.Runtime.InteropServices.RuntimeInformation.OSDescription, Checks = new[] { "Manual login flags", "Local recording connection", "Audio routing configuration", "Shared browser scripts", "Native Playwright driver startup", "JSON conversion", "Selection/status notifications", "Portable filenames", "WAV format and resume", "Site isolation", "Non-overwrite and traversal rejection", "Repeat recording, renamed tracks, failed retries, damaged/missing files and cancellation" } }, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }
        catch (Exception ex) { File.WriteAllText(Path.Combine(directory, "launch-check.json"), ex.ToString()); return 1; }
    }
    internal static void WriteTone(string path, int seconds)
    {
        using var file = new FileStream(path, FileMode.CreateNew);
        using var writer = new WaveFileWriter(file, new WaveFormat(48000, 16, 2));
        var samples = new byte[seconds * 192000];
        for (var frame = 0; frame < seconds * 48000; frame++)
        {
            var value = (short)(Math.Sin(frame * 2 * Math.PI * 440 / 48000) * 4000);
            System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(samples.AsSpan(frame * 4), value);
            System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(samples.AsSpan(frame * 4 + 2), value);
        }
        writer.Write(samples, 0, samples.Length);
    }
    public static async Task<int> AudioAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        try
        {
            if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("This unattended audio test requires a Linux PulseAudio/PipeWire session. macOS capture needs interactive permission.");
            await PlatformAudio.PrepareBrowserAsync();
            var tone = Path.Combine(directory, "tone-" + Guid.NewGuid().ToString("N") + ".wav"); WriteTone(tone, 3);
            var recorded = Path.Combine(directory, "capture-" + Guid.NewGuid().ToString("N") + ".wav");
            var capture = await WavCapture.StartAsync(Environment.ProcessId, recorded);
            try { await PlatformAudio.Command("paplay", "--device=" + PlatformAudio.Sink, tone); await Task.Delay(200); }
            finally { await capture.DisposeAsync(); }
            if (capture.Fault != null) throw capture.Fault;
            if (!capture.HasAudio || capture.Seconds < 2.9 || capture.Seconds > 7) throw new Exception($"Capture test failed: {capture.Seconds:F2} seconds, audible={capture.HasAudio}");
            using var reader = new WaveFileReader(recorded);
            if (reader.WaveFormat.SampleRate != 48000 || reader.WaveFormat.BitsPerSample != 16 || reader.WaveFormat.Channels != 2) throw new Exception("Wrong recorded PCM format.");
            File.WriteAllText(Path.Combine(directory, "audio-check.json"), JsonSerializer.Serialize(new { Passed = true, capture.Seconds, capture.HasAudio, Wav = recorded }));
            return 0;
        }
        catch (Exception ex) { File.WriteAllText(Path.Combine(directory, "audio-check.json"), ex.ToString()); return 1; }
        finally { await PlatformAudio.ReleaseBrowserAsync(); }
    }
}

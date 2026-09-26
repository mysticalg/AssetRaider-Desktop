using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.Text.Json;

namespace AssetRaider.Desktop;

public static class SelfTest
{
    public static int CheckLaunch(string directory)
    {
        Directory.CreateDirectory(directory);
        try
        {
            var login = MusicBrowser.LaunchOptions("chrome.exe", @"C:\Example Profile", false).ArgumentList;
            var recording = MusicBrowser.LaunchOptions("chrome.exe", @"C:\Example Profile", true).ArgumentList;
            if (login.Any(a => a.Contains("debugging") || a.Contains("automation"))) throw new Exception("Manual sign-in unexpectedly enables browser automation.");
            if (!recording.Contains("--remote-debugging-address=127.0.0.1") || !recording.Contains("--remote-debugging-port=0")) throw new Exception("Recording mode is not confined to loopback.");
            if (login[0] != recording[0]) throw new Exception("Login and recording profiles differ.");
            var suno = MusicBrowser.LaunchOptions("chrome.exe", @"C:\Suno Profile", false, MusicSite.Suno).ArgumentList;
            if (suno.Last() != "https://suno.com/create" || suno.Any(a => a.Contains("debugging"))) throw new Exception("Suno sign-in is misconfigured.");
            var scroll = BrowserData.Decode<ScrollState>("{\"Bottom\":true,\"Position\":420}");
            var songs = BrowserData.Decode<Track[]>("[{\"Site\":\"suno\",\"Id\":\"11111111-1111-4111-8111-111111111111\",\"Title\":\"Song\",\"Duration\":\"3:18\"}]");
            var player = BrowserData.Decode<PlayerState>("{\"Duration\":198,\"Elapsed\":2.5,\"Done\":false,\"Error\":\"\"}");
            if (!scroll.Bottom || scroll.Position != 420 || songs[0].Site != "suno" || player.Elapsed != 2.5) throw new Exception("Page JSON conversion failed.");
            if (!songs[0].Url.StartsWith("https://suno.com/song/") || MusicSite.Suno.OwnsUrl("https://suno.com.evil.test/create")) throw new Exception("Site isolation failed.");
            CheckResume(Path.Combine(directory, "resume-" + Guid.NewGuid().ToString("N")));
            RecordingPolicyCheck.RunAsync(Path.Combine(directory, "repeat-" + Guid.NewGuid().ToString("N"))).GetAwaiter().GetResult();
            File.WriteAllText(Path.Combine(directory, "launch-check.json"), JsonSerializer.Serialize(new { Passed = true, LoginArguments = login, RecordingArguments = recording, SunoArguments = suno, StructuredResults = "Scroll, track list and playback state decoded successfully", Resume = "Legacy Udio completion list migrated; identical song IDs remain separate across sites", RepeatRecording = "Both sites: resume path, renamed tracks, explicit repeat, failed retry preservation, numbered copies, missing/damaged files, cancellation" }, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }
        catch (Exception ex) { File.WriteAllText(Path.Combine(directory, "launch-check.json"), ex.ToString()); return 1; }
    }
    private static void CheckResume(string directory)
    {
        Directory.CreateDirectory(directory);
        var udio = new Track { Id = "11111111-1111-4111-8111-111111111111", Title = "Same title" };
        var suno = new Track { Id = udio.Id, Title = udio.Title, Site = "suno" };
        var oldWav = Path.Combine(directory, "existing.wav");
        using (var writer = new WaveFileWriter(oldWav, new WaveFormat(48000, 16, 2))) writer.Write(new byte[384000]);
        var oldLedger = new Dictionary<string, Completed> { [udio.Id] = new(udio.Id, udio.Title, "existing.wav", new FileInfo(oldWav).Length, 2) };
        File.WriteAllText(Path.Combine(directory, "assetraider-completed.json"), JsonSerializer.Serialize(oldLedger));
        var library = new RecordingLibrary(directory); library.Load();
        if (!library.IsComplete(udio) || library.IsComplete(suno)) throw new Exception("Legacy completion migration or cross-site isolation failed.");
        var names = library.Allocate(suno);
        if (names.Final == library.Allocate(udio).Final) throw new Exception("Cross-site output filenames collide.");
        using (var writer = new WaveFileWriter(names.Partial, new WaveFormat(48000, 16, 2))) writer.Write(new byte[384000]);
        library.Complete(suno, names.Partial, names.Final, 2);
        var reloaded = new RecordingLibrary(directory); reloaded.Load();
        if (!reloaded.IsComplete(udio) || !reloaded.IsComplete(suno)) throw new Exception("Saved site-specific completions did not survive reload.");
    }
    public static async Task<int> RunAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        var results = new List<string>();
        try
        {
            var track = new Track { Id = "ExampleUdioTrack000001", Title = "CON: bad/name? 🎵" };
            var library = new RecordingLibrary(directory); library.Load();
            var paths = library.Allocate(track);
            if (!paths.Final.StartsWith(Path.GetFullPath(directory)) || Path.GetFileName(paths.Final).Contains('?')) throw new Exception("Unsafe filename.");
            results.Add("Safe Unicode filenames and stable song IDs");
            var capture = await WavCapture.StartAsync(Environment.ProcessId, paths.Partial);
            using var player = new WasapiPlayerBuilder().WithSharedMode().WithEventSync().Build();
            player.Init(new SignalGenerator(48000, 2) { Frequency = 440, Gain = .08, Type = SignalGeneratorType.Sin }.ToWaveProvider());
            player.Play();
            await Task.Delay(3000);
            player.Stop();
            await Task.Delay(200);
            await capture.DisposeAsync();
            if (capture.Fault != null) throw capture.Fault;
            if (!capture.HasAudio || capture.Seconds < 2.8 || capture.Seconds > 3.8) throw new Exception($"Audio loopback failed: {capture.Seconds:F2}s, audible={capture.HasAudio}");
            results.Add($"Real process-loopback capture: {capture.Seconds:F2}s audible PCM");
            library.Complete(track, paths.Partial, paths.Final, 3);
            var resumed = new RecordingLibrary(directory); resumed.Load();
            if (!resumed.IsComplete(track)) throw new Exception("Resume did not recognize completed WAV.");
            if (resumed.IsComplete(new Track { Id = track.Id, Site = "suno" })) throw new Exception("Suno and Udio completion IDs collided.");
            if (resumed.Allocate(track).Final == paths.Final) throw new Exception("Existing WAV would be overwritten.");
            results.Add("Completed WAV validation, persisted resume and non-overwriting filenames");
            var unsafeTrack = new Track { Id = "../../escape", Title = "Unsafe" };
            try { RecordingLibrary.SafeName(unsafeTrack); throw new Exception("Traversal accepted."); } catch (ArgumentException) { }
            var incomplete = new Track { Id = "incomplete123", Title = "Interrupted" };
            var incompletePaths = library.Allocate(incomplete);
            using (var writer = new WaveFileWriter(incompletePaths.Partial, new WaveFormat(48000,16,2))) writer.Write(new byte[19200]);
            if (resumed.IsComplete(incomplete)) throw new Exception("Partial WAV marked complete.");
            try { library.Complete(incomplete, incompletePaths.Partial, incompletePaths.Final, 3); throw new Exception("Short WAV accepted."); } catch (IOException) { }
            results.Add("Rejected path traversal and incomplete WAV completion");
            File.WriteAllText(Path.Combine(directory, "self-test.json"), JsonSerializer.Serialize(new { Passed = true, Results = results, Wav = paths.Final }, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(directory, "self-test.json"), JsonSerializer.Serialize(new { Passed = false, Results = results, Error = ex.ToString() }, new JsonSerializerOptions { WriteIndented = true }));
            return 1;
        }
    }
}

using NAudio.Wave;

namespace AssetRaider.Desktop;

// Exercises the real queue without starting Chrome or recording the user's audio.
internal static class RecordingPolicyCheck
{
    public static async Task RunAsync(string directory)
    {
        foreach (var site in new[] { "udio", "suno" })
        {
            var folder = Path.Combine(directory, site);
            var library = new RecordingLibrary(folder); library.Load();
            var track = new Track { Id = "RepeatRecording0001", Title = "Original title", Site = site };
            var paths = library.Allocate(track);
            WriteWav(paths.Partial);
            library.Complete(track, paths.Partial, paths.Final, 2);
            var original = File.ReadAllBytes(paths.Final);
            var ledger = File.ReadAllText(Path.Combine(folder, "assetraider-completed.json"));
            var browser = new ProbeBrowser(MusicSite.FromId(site));
            var queue = new RecorderQueue(browser);
            var messages = new List<string>();
            void Update(Track _, string message, double fraction) => messages.Add(message);

            // A renamed song still has the same ID; show the real, older filename.
            track.Title = "Renamed title";
            await queue.RunAsync([track], folder, Update, CancellationToken.None);
            Require(browser.Attempts == 0 && messages.Single().Contains(paths.Final), "Resume must skip and identify the existing WAV, including after a rename.");

            messages.Clear();
            await queue.RunAsync([track], folder, Update, CancellationToken.None, recordAgain: true);
            Require(browser.Attempts == 1 && messages.Contains("Failed: Test playback unavailable"), "Record again must attempt fresh playback even when a valid WAV exists.");
            Require(File.ReadAllBytes(paths.Final).SequenceEqual(original) && File.ReadAllText(Path.Combine(folder, "assetraider-completed.json")) == ledger,
                "A failed repeat recording must preserve the previous WAV and completion entry.");

            track.Title = "Original title";
            var repeat = library.Allocate(track);
            Require(repeat.Final != paths.Final, "Repeat recording would overwrite the original.");
            WriteWav(repeat.Partial);
            Require(library.Allocate(track).Final != repeat.Final, "Interrupted repeat recording would be overwritten.");
            library.Complete(track, repeat.Partial, repeat.Final, 2);
            var reloaded = new RecordingLibrary(folder); reloaded.Load();
            Require(reloaded.CompletedPath(track) == repeat.Final && File.ReadAllBytes(paths.Final).SequenceEqual(original), "Completed repeat must point to the new copy and preserve the old WAV.");

            File.WriteAllBytes(repeat.Final, new byte[12]);
            await queue.RunAsync([track], folder, Update, CancellationToken.None);
            Require(browser.Attempts == 2, "A damaged WAV must not be skipped.");
            File.Delete(repeat.Final);
            await queue.RunAsync([track], folder, Update, CancellationToken.None);
            Require(browser.Attempts == 3, "A missing WAV must not be skipped.");

            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            try { await queue.RunAsync([track], folder, Update, cancelled.Token, recordAgain: true); throw new Exception("Cancelled repeat started playback."); }
            catch (OperationCanceledException) { }
            Require(browser.Attempts == 3, "Cancelled repeat attempted playback.");
        }
    }

    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void WriteWav(string path)
    {
        using var file = new FileStream(path, FileMode.CreateNew);
        using var writer = new WaveFileWriter(file, new WaveFormat(48000, 16, 2));
        writer.Write(new byte[384000]);
    }
    private sealed class ProbeBrowser(MusicSite site) : IRecordingBrowser
    {
        public MusicSite Site => site;
        public int ProcessId => throw new Exception("Test must not start audio capture.");
        public int Attempts { get; private set; }
        public Task<double> PrepareAsync(Track track, CancellationToken cancellation)
        {
            Attempts++;
            throw new IOException("Test playback unavailable");
        }
        public Task StartAsync() => throw new Exception("Unexpected playback.");
        public Task<PlayerState> StateAsync() => throw new Exception("Unexpected playback polling.");
        public Task StopAsync() => Task.CompletedTask;
    }
}

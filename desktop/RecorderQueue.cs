using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AssetRaider.Desktop;

public interface IRecordingBrowser
{
    MusicSite Site { get; }
    int ProcessId { get; }
    Task<double> PrepareAsync(Track track, CancellationToken cancellation);
    Task StartAsync();
    Task<PlayerState> StateAsync();
    Task StopAsync();
}

public sealed class RecorderQueue(IRecordingBrowser browser)
{
    [DllImport("kernel32.dll")] private static extern uint SetThreadExecutionState(uint flags);
    public async Task RunAsync(Track[] tracks, string directory, Action<Track, string, double> update, CancellationToken cancellation, bool recordAgain = false)
    {
        var library = new RecordingLibrary(directory);
        library.Load();
        // Prevent automatic sleep while a queue is running. This call and its reset run on the UI thread.
        if (OperatingSystem.IsWindows()) SetThreadExecutionState(0x80000001);
        try
        {
            foreach (var track in tracks)
            {
                cancellation.ThrowIfCancellationRequested();
                if (!recordAgain && library.CompletedPath(track) is { } savedPath)
                {
                    update(track, "Already saved: " + savedPath + " — use Record again (new copy) to re-record.", 1);
                    continue;
                }
                try
                {
                    update(track, "Preparing playback…", 0);
                    var duration = await browser.PrepareAsync(track, cancellation);
                    cancellation.ThrowIfCancellationRequested();
                    var root = Path.GetPathRoot(Path.GetFullPath(directory))!;
                    if (new DriveInfo(root).AvailableFreeSpace < duration * 192000 + 50_000_000) throw new IOException("Not enough free disk space for this song.");
                    var paths = library.Allocate(track);
                    var capture = await WavCapture.StartAsync(browser.ProcessId, paths.Partial);
                    try
                    {
                        await browser.StartAsync();
                        var clock = Stopwatch.StartNew();
                        double previous = 0;
                        while (true)
                        {
                            cancellation.ThrowIfCancellationRequested();
                            if (capture.Fault != null) throw capture.Fault;
                            var state = await browser.StateAsync().WaitAsync(TimeSpan.FromSeconds(10), cancellation);
                            if (state.Error.Length > 0) throw new IOException(state.Error);
                            if (state.Elapsed + .3 < previous && !state.Done) throw new IOException("Playback jumped backwards. Recording kept as partial.");
                            if (state.Elapsed > previous + 3) throw new IOException("Playback jumped forwards. Recording kept as partial.");
                            previous = state.Elapsed;
                            update(track, $"Recording {TimeSpan.FromSeconds(state.Elapsed):m\\:ss} / {TimeSpan.FromSeconds(duration):m\\:ss}", Math.Clamp(state.Elapsed / duration, 0, 1));
                            if (state.Done) break;
                            if (clock.Elapsed.TotalSeconds > duration + 35) throw new IOException("The song did not finish within its recording time limit.");
                            await Task.Delay(250, cancellation);
                        }
                        await Task.Delay(150, cancellation); // Drain the final output audio packet.
                    }
                    finally { await browser.StopAsync(); await capture.DisposeAsync(); }
                    cancellation.ThrowIfCancellationRequested();
                    if (capture.Fault != null) throw capture.Fault;
                    if (!capture.HasAudio) throw new IOException($"No audible audio was captured. Check that {browser.Site.Name} plays in the app browser. Kept as partial.");
                    if (Math.Abs(capture.Seconds - duration) > 1.5) throw new IOException($"Capture duration ({capture.Seconds:F1}s) differs from the song ({duration:F1}s). Kept as partial; retry when playback is stable.");
                    library.Complete(track, paths.Partial, paths.Final, duration);
                    update(track, "Saved WAV", 1);
                }
                catch (OperationCanceledException) { update(track, "Stopped — partial kept if captured", 0); throw; }
                catch (Exception ex) { update(track, "Failed: " + ex.Message, 0); }
                finally { await browser.StopAsync(); }
            }
        }
        finally { if (OperatingSystem.IsWindows()) SetThreadExecutionState(0x80000000); }
    }
}

namespace AssetRaider.Desktop;

// UI operations and polling are serialized; recordings take ownership only after StopAsync.
public sealed class PreviewPlayer(MusicBrowser browser)
{
    public Track? Track { get; private set; }
    public PlayerState? State { get; private set; }
    public string? SpectrumError { get; private set; }
    private WavCapture? monitor;
    public async Task PlayAsync(Track track, CancellationToken cancellation)
    {
        await StopAsync();
        try {
            await browser.PreparePreviewAsync(track, cancellation);
            await PlatformAudio.SetListeningAsync(true);
            try { monitor = await WavCapture.StartMonitorAsync(browser.ProcessId); }
            catch (Exception ex) { SpectrumError = ex.Message; }
            cancellation.ThrowIfCancellationRequested();
            await browser.StartAsync(); Track = track; await PollAsync();
        }
        catch { await StopAsync(); throw; }
    }
    public async Task ToggleAsync()
    {
        if (Track == null) return;
        if (State?.Paused == true || State?.Done == true) await browser.StartAsync(); else await browser.PausePlaybackAsync();
        await PollAsync();
    }
    public async Task SeekAsync(double seconds) { if (Track != null) { await browser.SeekPlaybackAsync(seconds); await PollAsync(); } }
    public async Task PollAsync()
    {
        if (Track == null) return;
        State = await browser.StateAsync().WaitAsync(TimeSpan.FromSeconds(5));
        if (State.Error.Length > 0) throw new IOException(State.Error);
        Track.SourceQuality = browser.SourceQuality;
        Track.PlaybackAction = State.Done ? "Replay" : State.Paused ? "Resume" : "Pause";
        if (monitor?.Fault != null) SpectrumError = monitor.Fault.Message;
    }
    public async Task StopAsync()
    {
        var old = Track; Track = null; State = null; SpectrumError = null;
        if (old != null) old.PlaybackAction = "Play";
        await browser.StopAsync();
        var capture = monitor; monitor = null;
        try { if (capture != null) await capture.DisposeAsync(); }
        finally {
            try { await PlatformAudio.SetListeningAsync(false); }
            finally { AudioSpectrum.Shared.Reset(); }
        }
    }
}

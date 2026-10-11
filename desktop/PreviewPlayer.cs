namespace AssetRaider.Desktop;

// UI operations and polling are serialized; recordings take ownership only after StopAsync.
public sealed class PreviewPlayer(MusicBrowser browser)
{
    public Track? Track { get; private set; }
    public PlayerState? State { get; private set; }
    public string? SpectrumError { get; private set; }
    public bool IsBuffering => loading || State?.Buffering == true;
    public event Action? Changed;
    public PlaybackPlaylist Playlist { get; } = new();
    private bool loading;
    private WavCapture? monitor;
    public async Task PlayAsync(Track track, CancellationToken cancellation)
    {
        await StopAsync();
        Track = track; loading = true; UpdatePresentation();
        try {
            await browser.PreparePreviewAsync(track, cancellation);
            await PlatformAudio.SetListeningAsync(true);
            try { monitor = await WavCapture.StartMonitorAsync(browser.ProcessId); }
            catch (Exception ex) { SpectrumError = ex.Message; }
            cancellation.ThrowIfCancellationRequested();
            await browser.StartAsync(); Playlist.Started(track); loading = false; await PollAsync();
        }
        catch { await StopAsync(); throw; }
    }
    public async Task ToggleAsync()
    {
        if (Track == null) return;
        var resume = State?.Paused == true || State?.Done == true;
        if (resume) { loading = true; UpdatePresentation(); }
        try {
            if (resume) await browser.StartAsync(); else await browser.PausePlaybackAsync();
            loading = false; await PollAsync();
        }
        finally { loading = false; UpdatePresentation(); }
    }
    public async Task SeekAsync(double seconds) { if (Track != null) { await browser.SeekPlaybackAsync(seconds); await PollAsync(); } }
    public async Task NextAsync(bool automatic = false)
    {
        if (Track == null) return;
        var next = Playlist.Next(Track, automatic);
        if (next == null) return;
        if (automatic && next.Key == Track.Key && State?.Done == true) await ToggleAsync();
        else await PlayAsync(next, CancellationToken.None);
    }
    public async Task PollAsync()
    {
        if (Track == null) return;
        State = await browser.StateAsync().WaitAsync(TimeSpan.FromSeconds(5));
        if (State.Error.Length > 0) throw new IOException(State.Error);
        Track.SourceQuality = browser.SourceQuality;
        if (monitor?.Fault != null) SpectrumError = monitor.Fault.Message;
        UpdatePresentation();
    }
    private void UpdatePresentation()
    {
        if (Track != null) Track.PlaybackAction = IsBuffering ? "Buffering…" : State?.Done == true ? "Replay" : State?.Paused == true ? "Resume" : "Pause";
        Changed?.Invoke();
    }
    public async Task StopAsync()
    {
        var old = Track; Track = null; State = null; SpectrumError = null; loading = false;
        if (old != null) old.PlaybackAction = "Play";
        UpdatePresentation();
        await browser.StopAsync();
        var capture = monitor; monitor = null;
        try { if (capture != null) await capture.DisposeAsync(); }
        finally {
            try { await PlatformAudio.SetListeningAsync(false); }
            finally { AudioSpectrum.Shared.Reset(); }
        }
    }
}

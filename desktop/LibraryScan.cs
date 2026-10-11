using System.Diagnostics;

namespace AssetRaider.Desktop;

public sealed class ScanControl
{
    private TaskCompletionSource? resume;
    private readonly Stopwatch clock = Stopwatch.StartNew();
    public bool PauseRequested => resume != null;
    public TimeSpan ActiveTime => clock.Elapsed;
    public Action? Paused { get; set; }
    public void Pause() { if (resume != null) return; resume = new(TaskCreationOptions.RunContinuationsAsynchronously); clock.Stop(); }
    public void Resume() { var waiter = resume; resume = null; clock.Start(); waiter?.TrySetResult(); }
    public async Task CheckpointAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        while (resume is { } waiter)
        {
            Paused?.Invoke();
            await waiter.Task.WaitAsync(token);
        }
        token.ThrowIfCancellationRequested();
    }
}

public interface ILibraryScanSource
{
    Task ResetAsync();
    Task<Track[]> ReadAsync();
    Task<ScrollState> ScrollAsync();
    Task<bool> ChangePageAsync(string direction, CancellationToken token);
}

public static class LibraryScanner
{
    public static async Task<string> RunAsync(ILibraryScanSource source, ScanControl control, Action<Track[]> found,
        CancellationToken token, TimeSpan? interval = null)
    {
        var seen = new HashSet<string>();
        async Task Check() {
            await control.CheckpointAsync(token);
            if (control.ActiveTime >= TimeSpan.FromMinutes(10)) throw new IOException("Scan reached its 10-minute active-time limit. Tracks found remain available; use a smaller filtered view and scan again.");
        }
        await Check(); await source.ResetAsync();
        while (true)
        {
            await Check();
            if (!await source.ChangePageAsync("previous", token)) break;
        }
        var unchanged = 0;
        while (true)
        {
            await Check();
            var tracks = await source.ReadAsync();
            await Check();
            var fresh = tracks.Where(t => seen.Add(t.Key)).ToArray();
            if (fresh.Length > 0) { found(fresh); unchanged = 0; }
            await Check();
            var scroll = await source.ScrollAsync();
            unchanged = fresh.Length == 0 && scroll.Bottom ? unchanged + 1 : 0;
            if (unchanged >= 10)
            {
                await Check();
                if (!await source.ChangePageAsync("next", token))
                    return $"Loaded {seen.Count} tracks. Reached the end of the current filtered library view.";
                unchanged = 0;
                await Check(); await source.ResetAsync();
            }
            await Task.Delay(interval ?? TimeSpan.FromMilliseconds(800), token);
        }
    }
}

public sealed class LibraryCatalog
{
    private readonly Dictionary<string, Track> tracks = [];
    public int Count => tracks.Count;
    public void Clear() => tracks.Clear();
    public bool Add(Track track) => tracks.TryAdd(track.Key, track);
    public Track[] Matching(string? query) => tracks.Values.Where(t => t.Title.Contains((query ?? "").Trim(), StringComparison.OrdinalIgnoreCase)).ToArray();
}

namespace AssetRaider.Desktop;

internal static class LibraryScanCheck
{
    public static async Task RunAsync()
    {
        Require(MusicSite.Suno.LibraryUrl == "https://suno.com/me", "Suno must open the user's library.");
        foreach (var url in new[] { "https://suno.com/me?filter=liked", "https://suno.com/create", "https://www.suno.com/me/" })
            Require(MusicSite.Suno.IsLibraryUrl(url), "A supported library view would be reset.");
        foreach (var url in new[] { "https://suno.com/memes", "https://suno.com/create-fake", "https://suno.com.evil.test/me", "https://suno.com/song/example" })
            Require(!MusicSite.Suno.IsLibraryUrl(url), "An unrelated route was accepted as a library.");

        var catalog = new LibraryCatalog();
        var first = new Track { Id = "ExampleTrack0001", Title = "Night Drive", Site = "suno", Selected = true };
        catalog.Add(first); catalog.Add(new() { Id = "ExampleTrack0002", Title = "Sunrise", Site = "suno" });
        Require(!catalog.Add(first), "Duplicate track inserted.");
        Require(catalog.Matching(" NIGHT ").Single() == first, "Name search must trim and ignore case.");
        Require(catalog.Matching("sun").All(t => !t.Selected), "Hidden selections must not enter the visible recording queue.");
        Require(catalog.Matching("").Length == 2 && catalog.Matching("missing").Length == 0, "Search reset/empty results failed.");
        first.Duration = "10:00"; first.Status = "Saved WAV";
        var second = catalog.Matching("sun").Single(); second.Duration = "9:59"; second.Status = "Ready";
        Require(catalog.Matching("",TrackSort.Song,true).First() == second, "Song descending order failed.");
        Require(catalog.Matching("",TrackSort.Length).First() == second && catalog.Matching("",TrackSort.Length,true).First() == first, "Duration was sorted as text.");
        Require(catalog.Matching("",TrackSort.Status).First() == second && first.Selected, "Status sort lost track selection.");
        catalog.Add(new() {Id="ExampleTrack0003",Title="No duration"});
        Require(catalog.Matching("",TrackSort.Length).Last().Duration == "" && catalog.Matching("",TrackSort.Length,true).Last().Duration == "", "Unknown lengths were not kept last.");
        Require(catalog.Matching("sun",TrackSort.Length).Single() == second, "Sorting changed the name filter.");

        var source = new FakeSource();
        var control = new ScanControl();
        var paused = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        control.Paused = () => paused.TrySetResult();
        var collected = new List<string>();
        var run = LibraryScanner.RunAsync(source, control, fresh => {
            collected.AddRange(fresh.Select(t => t.Id));
            if (collected.Count == 1) control.Pause();
        }, CancellationToken.None, TimeSpan.Zero);
        await paused.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Require(!run.IsCompleted && source.Reads == 1 && source.Scrolls == 0, "Pause must stop before the next browser step.");
        var active = control.ActiveTime; await Task.Yield();
        Require(active == control.ActiveTime, "Paused time must not consume the scan time limit.");
        control.Resume();
        await run.WaitAsync(TimeSpan.FromSeconds(3));
        Require(collected.SequenceEqual(new[] { "track001", "track002", "track003" }), "Resume must retain its position and deduplicate across pages.");
        Require(source.Resets == 2, "Resume must not restart the scan.");

        source = new FakeSource(); control = new(); control.Pause();
        using var cancelled = new CancellationTokenSource();
        run = LibraryScanner.RunAsync(source, control, _ => throw new Exception("Results arrived after clear/stop."), cancelled.Token, TimeSpan.Zero);
        cancelled.Cancel();
        try { await run; throw new Exception("Paused cancellation failed."); } catch (OperationCanceledException) { }
        Require(source.Reads == 0 && source.Resets == 0, "Cancelled pause touched the page.");
        catalog.Clear(); Require(catalog.Count == 0 && catalog.Matching("").Length == 0, "Clear left stale search results.");
        var delayed = new DelayedSource();
        using var stopping = new CancellationTokenSource();
        run = LibraryScanner.RunAsync(delayed, new(), _ => throw new Exception("Late browser results escaped cancellation."), stopping.Token, TimeSpan.Zero);
        await delayed.Started.Task;
        stopping.Cancel(); delayed.Result.SetResult([first]);
        try { await run; throw new Exception("In-flight scan ignored cancellation."); } catch (OperationCanceledException) { }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    private sealed class DelayedSource : ILibraryScanSource
    {
        public readonly TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource<Track[]> Result = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task ResetAsync() => Task.CompletedTask;
        public Task<Track[]> ReadAsync() { Started.SetResult(); return Result.Task; }
        public Task<ScrollState> ScrollAsync() => throw new Exception("Cancelled scan scrolled.");
        public Task<bool> ChangePageAsync(string direction, CancellationToken token) => Task.FromResult(false);
    }
    private sealed class FakeSource : ILibraryScanSource
    {
        private int page, row;
        public int Reads, Scrolls, Resets;
        public Task ResetAsync() { Resets++; row = 0; return Task.CompletedTask; }
        public Task<Track[]> ReadAsync() { Reads++; return Task.FromResult(new[] { new Track { Id = $"track00{page + Math.Min(row, 1) + 1}", Title = "Example", Site = "suno" } }); }
        public Task<ScrollState> ScrollAsync() { Scrolls++; row++; return Task.FromResult(new ScrollState(row >= 1, row)); }
        public Task<bool> ChangePageAsync(string direction, CancellationToken token)
        {
            if (direction == "next" && page == 0) { page++; return Task.FromResult(true); }
            return Task.FromResult(false);
        }
    }
}

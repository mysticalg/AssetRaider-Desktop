using System.Diagnostics;
using System.Reflection;
using Microsoft.Playwright;

namespace AssetRaider.Desktop;

public sealed record PlayerState(double Duration, double Elapsed, bool Done, string Error);
public sealed record ScrollState(bool Bottom, double Position);
public sealed record PageChange(bool Changed, string Error);

public sealed class MusicBrowser : IAsyncDisposable
{
    public MusicSite Site { get; private set; } = MusicSite.Udio;
    private IPlaywright? playwright;
    private IBrowser? browser;
    private IPage? page;
    private Process? process;
    private Process? loginProcess;
    public int ProcessId => process is { HasExited: false } ? process.Id : throw new IOException("The app's Chrome window was closed. Load the library again.");
    public static string AppData => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AssetRaider");
    public static string Script(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"AssetRaider.Desktop.Scripts.{name}.js")!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
    private static string ChromePath()
    {
        if (OperatingSystem.IsMacOS())
            return new[] { "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Applications/Google Chrome.app/Contents/MacOS/Google Chrome") }.FirstOrDefault(File.Exists)
                ?? throw new FileNotFoundException("Install Google Chrome in Applications to use the recorder.");
        if (OperatingSystem.IsLinux())
            return new[] { "/usr/bin/google-chrome", "/usr/bin/google-chrome-stable", "/opt/google/chrome/chrome" }.FirstOrDefault(File.Exists)
                ?? throw new FileNotFoundException("Install the native Google Chrome .deb or .rpm package. Snap/Flatpak browsers are not supported.");
        return new[] {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Google\Chrome\Application\chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Google\Chrome\Application\chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Google\Chrome\Application\chrome.exe")
        }.FirstOrDefault(File.Exists) ?? throw new FileNotFoundException("Install Google Chrome to use the recorder.");
    }
    public static ProcessStartInfo LaunchOptions(string chrome, string profile, bool recording, MusicSite? site = null)
    {
        var start = new ProcessStartInfo(chrome) { UseShellExecute = false };
        start.ArgumentList.Add($"--user-data-dir={profile}");
        if (recording)
        {
            start.ArgumentList.Add("--remote-debugging-port=0");
            start.ArgumentList.Add("--remote-debugging-address=127.0.0.1");
            PlatformAudio.ConfigureBrowser(start);
        }
        start.ArgumentList.Add("--disable-background-mode");
        start.ArgumentList.Add("--new-window");
        start.ArgumentList.Add((site ?? MusicSite.Udio).LibraryUrl);
        return start;
    }
    public async Task OpenLoginAsync()
    {
        if (loginProcess is { HasExited: false }) throw new IOException("The normal Chrome sign-in window is already open. Finish signing in there, then close that window and click Load library.");
        await DisposeAsync();
        var profile = Path.Combine(AppData, Site.ProfileFolder);
        Directory.CreateDirectory(profile);
        // Authentication is entirely manual in ordinary Chrome. No debugging port or Playwright connection exists in this mode.
        loginProcess = Process.Start(LaunchOptions(ChromePath(), profile, false, Site)) ?? throw new IOException("Could not open Chrome for sign-in.");
    }
    public async Task SelectSiteAsync(MusicSite site)
    {
        if (site == Site) return;
        if (loginProcess is { HasExited: false }) throw new IOException("Close the separate sign-in Chrome window before changing sites.");
        await DisposeAsync();
        Site = site;
    }
    private async Task ConnectAsync()
    {
        if (browser?.IsConnected == true && page is { IsClosed: false }) return;
        if (loginProcess is { HasExited: false }) throw new IOException($"After signing into {Site.Name}, close its separate Chrome sign-in window, then click Load library again. This lets Chrome save the login before recording starts.");
        await DisposeAsync();
        var profile = Path.Combine(AppData, Site.ProfileFolder);
        Directory.CreateDirectory(profile);
        var portFile = Path.Combine(profile, "DevToolsActivePort");
        if (File.Exists(portFile)) File.Delete(portFile);
        await PlatformAudio.PrepareBrowserAsync();
        process = Process.Start(LaunchOptions(ChromePath(), profile, true, Site)) ?? throw new IOException("Could not start Chrome.");
        var deadline = DateTime.UtcNow.AddSeconds(30);
        string? port = null;
        while (DateTime.UtcNow < deadline)
        {
            if (process.HasExited) throw new IOException("Chrome could not start its separate profile. Close any previous AssetRaider browser window and retry.");
            try { if (File.Exists(portFile)) port = (await File.ReadAllLinesAsync(portFile)).FirstOrDefault(); } catch (IOException) { }
            if (int.TryParse(port, out _)) break;
            await Task.Delay(200);
        }
        if (!int.TryParse(port, out var portNumber)) throw new IOException("Chrome did not become ready within 30 seconds.");
        playwright = await Playwright.CreateAsync();
        browser = await playwright.Chromium.ConnectOverCDPAsync($"http://127.0.0.1:{portNumber}", new() { Timeout = 20000 });
        page = browser.Contexts.SelectMany(c => c.Pages).FirstOrDefault(p => Site.OwnsUrl(p.Url))
            ?? await browser.Contexts[0].NewPageAsync();
        page.SetDefaultTimeout(20000);
        page.SetDefaultNavigationTimeout(30000);
        if (!Site.OwnsUrl(page.Url)) await page.GotoAsync(Site.LibraryUrl);
    }
    private IPage Page => browser?.IsConnected == true && page is { IsClosed: false } ? page : throw new IOException("Sign in, then Load library first.");

    public async Task<string> ScanAsync(Action<Track[]> found, CancellationToken cancellation)
    {
        await ConnectAsync();
        cancellation.ThrowIfCancellationRequested();
        var tab = Page;
        if (!Site.IsLibraryUrl(tab.Url))
            await tab.GotoAsync(Site.LibraryUrl, new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        try { await tab.Locator(Site.LinkSelector).First.WaitForAsync(new() { Timeout = 20000 }); }
        catch (TimeoutException) { throw new IOException($"No {Site.Name} library tracks are visible. Use Sign in (normal Chrome), finish signing in and close that window, then Load library. Also check library filters and the selected workspace."); }
        await BrowserData.ReadAsync<ScrollState>(tab, Script("scroll"), new { reset = true, site = Site.Id });
        var seen = new HashSet<string>();
        var unchanged = 0;
        var deadline = DateTime.UtcNow.AddMinutes(10);
        if (Site.Id == "suno")
        {
            // Begin at the first page of the user's selected workspace. Pagination must actually advance.
            while (await ChangeSunoPageAsync(tab, "previous", cancellation))
                if (DateTime.UtcNow >= deadline) throw new IOException("Could not reach the first workspace page within the scan time limit.");
        }
        while (DateTime.UtcNow < deadline)
        {
            cancellation.ThrowIfCancellationRequested();
            var tracks = await BrowserData.ReadAsync<Track[]>(tab, Script("library"), new { site = Site.Id });
            var fresh = tracks.Where(t => seen.Add(t.Id)).ToArray();
            if (fresh.Length > 0) { found(fresh); unchanged = 0; }
            var scroll = await BrowserData.ReadAsync<ScrollState>(tab, Script("scroll"), new { reset = false, site = Site.Id });
            unchanged = fresh.Length == 0 && scroll.Bottom ? unchanged + 1 : 0;
            if (unchanged >= 10)
            {
                if (Site.Id == "suno" && await ChangeSunoPageAsync(tab, "next", cancellation))
                {
                    unchanged = 0;
                    await BrowserData.ReadAsync<ScrollState>(tab, Script("scroll"), new { reset = true, site = Site.Id });
                    continue;
                }
                return $"Loaded {seen.Count} tracks from {Site.Name}. Reached the end of this view. Filters, folders and the selected workspace affect this list.";
            }
            await Task.Delay(800, cancellation);
        }
        return $"Loaded {seen.Count} tracks. Scan reached its 10-minute limit; the list may be incomplete. Scan a smaller filtered library view to continue.";
    }

    private static async Task<bool> ChangeSunoPageAsync(IPage tab, string direction, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        var result = await BrowserData.ReadAsync<PageChange>(tab, Script("pagination"), new { direction }).WaitAsync(cancellation);
        if (result.Error.Length > 0) throw new IOException(result.Error + " Tracks already found remain available; this list may be incomplete.");
        return result.Changed;
    }

    public async Task<double> PrepareAsync(Track track, CancellationToken cancellation)
    {
        _ = RecordingLibrary.SafeName(track);
        if (track.Site != Site.Id) throw new IOException("The selected song belongs to another site. Load this site's library again.");
        await ConnectAsync();
        var tab = Page;
        // Reload each exact song URL so a previous player or autoplay queue cannot select another song.
        await tab.GotoAsync(track.Url, new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        await tab.Locator(Site.PlaySelector + ":visible").First.WaitForAsync();
        cancellation.ThrowIfCancellationRequested();
        return await tab.EvaluateAsync<double>(Script("player"), new { site = Site.Id, id = track.Id }).WaitAsync(cancellation);
    }
    public Task StartAsync() => Page.EvaluateAsync("() => window.__assetRaiderDesktop.start()");
    public Task<PlayerState> StateAsync() => BrowserData.ReadAsync<PlayerState>(Page, "() => window.__assetRaiderDesktop.state()");
    public async Task StopAsync()
    {
        try { if (page is { IsClosed: false }) await page.EvaluateAsync("() => window.__assetRaiderDesktop?.stop()").WaitAsync(TimeSpan.FromSeconds(5)); } catch { }
    }
    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        // Browser.close closes only the dedicated browser launched with this app's private profile.
        try
        {
            if (browser?.IsConnected == true)
            {
                var session = await browser.NewBrowserCDPSessionAsync().WaitAsync(TimeSpan.FromSeconds(5));
                await session.SendAsync("Browser.close").WaitAsync(TimeSpan.FromSeconds(5));
            }
        }
        catch { }
        if (process is { HasExited: false })
        {
            try { process.CloseMainWindow(); await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); } catch { }
        }
        var oldPlaywright = playwright;
        playwright = null; browser = null; page = null;
        if (oldPlaywright != null)
            try { await Task.Run(oldPlaywright.Dispose).WaitAsync(TimeSpan.FromSeconds(5)); } catch { }
        process?.Dispose(); process = null;
        await PlatformAudio.ReleaseBrowserAsync();
        // Manual sign-in is owned by the user; never close it while credentials are being entered.
        loginProcess?.Dispose(); loginProcess = null;
    }
}

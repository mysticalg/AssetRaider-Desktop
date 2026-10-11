using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace AssetRaider.Desktop;

public sealed class MainWindow : Window
{
    private readonly MusicBrowser browser = new();
    private readonly PreviewPlayer player;
    private readonly TextBlock nowPlaying = new() { Text = "Music player · click Play beside a track · Chrome can stay minimized", TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock playbackTime = new() { Text = "0:00 / 0:00", VerticalAlignment = VerticalAlignment.Center };
    private readonly Button playbackToggle = new() { Content = "Pause", IsEnabled = false };
    private readonly Button playbackStop = new() { Content = "Stop player", IsEnabled = false };
    private readonly Slider playbackSeek = new() { Minimum = 0, Maximum = 1000, IsEnabled = false };
    private readonly DispatcherTimer playerTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private bool playerBusy, pollingPlayer, seekingPlayer;
    private readonly ObservableCollection<Track> tracks = [];
    private readonly LibraryCatalog catalog = new();
    private readonly TextBox search = new() { Width = 270, Watermark = "Type a track name…" };
    private readonly Button pauseScan = new() { Content = "Pause scan", IsEnabled = false, Margin = new Thickness(0, 0, 8, 0) };
    private Button clearScan = null!;
    private ScanControl? scanControl;
    private bool clearing;
    private readonly ComboBox site = new() { ItemsSource = new[] { "Udio", "Suno" }, SelectedIndex = 0, Width = 125 };
    private readonly TextBox folder = new();
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock count = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly ProgressBar progress = new() { Minimum = 0, Maximum = 100, Height = 6 };
    private readonly TextBox log = new() { IsReadOnly = true, AcceptsReturn = true, Height = 100, TextWrapping = TextWrapping.Wrap };
    private readonly ListBox list = new();
    private readonly List<Button> idle = [];
    private readonly Button stop = new() { Content = "Stop", IsEnabled = false };
    private CancellationTokenSource? cancellation;
    private Task? operation;
    private bool closing, closeRequested, resettingSite;
    private string SettingsPath => Path.Combine(MusicBrowser.AppData, "settings.json");

    public MainWindow()
    {
        player = new(browser);
        Title = "AssetRaider 0.4.7 beta — Udio + Suno to WAV"; Width = 1120; Height = 960; MinWidth = 960; MinHeight = 840;
        Background = Brush.Parse("#101810");
        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,*,Auto,Auto,Auto,Auto,Auto,Auto,Auto"), Margin = new Thickness(24), RowSpacing = 10 };
        void Row(Control control, int row) { Grid.SetRow(control, row); root.Children.Add(control); }
        using var logoStream = typeof(MainWindow).Assembly.GetManifestResourceStream("AssetRaider.Brand.Icon.png")!;
        var logo = new Avalonia.Media.Imaging.Bitmap(logoStream); Icon = new WindowIcon(logo);
        Closed += (_, _) => logo.Dispose();
        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16, Margin = new Thickness(0, 0, 0, 8) };
        header.Children.Add(new Image { Source = logo, Width = 64, Height = 64 });
        var heading = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        heading.Children.Add(new TextBlock { Text = "AssetRaider", FontSize = 28, FontWeight = FontWeight.Bold });
        heading.Children.Add(new TextBlock { Text = "YOUR MUSIC, SAVED LOCALLY   /   Udio + Suno playback to WAV", FontSize = 12, Foreground = Brush.Parse("#BCD49F") });
        header.Children.Add(heading); Row(header, 0);
        var toolbar = new WrapPanel { Orientation = Orientation.Horizontal };
        toolbar.Children.Add(new TextBlock { Text = "Music site", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) });
        toolbar.Children.Add(site);
        toolbar.Children.Add(Button("Sign in (normal Chrome)", () => Work(async _ => { await browser.OpenLoginAsync(); SetStatus("Sign in in the separate Chrome window, then close it and click Scan library."); })));
        toolbar.Children.Add(Button("Open library / filters", () => Work(async _ => { await browser.OpenLibraryAsync(); SetStatus("Choose filters in Chrome, then Scan library. Stop or clear a scan before changing site filters."); })));
        Row(toolbar, 1);
        var scanBar = new WrapPanel { Orientation = Orientation.Horizontal };
        scanBar.Children.Add(Button("Scan library", () => Work(token => Scan(token))));
        pauseScan.Click += (_, _) => TogglePause(); scanBar.Children.Add(pauseScan);
        clearScan = Button("Clear scanned list", async () => await ClearScanAsync()); idle.Remove(clearScan); scanBar.Children.Add(clearScan);
        scanBar.Children.Add(Button("Select all", () => Select(true)));
        scanBar.Children.Add(Button("Select none", () => Select(false)));
        Row(scanBar, 2);
        var searchBar = new WrapPanel { Orientation = Orientation.Horizontal };
        searchBar.Children.Add(new TextBlock { Text = "Track name  ", VerticalAlignment = VerticalAlignment.Center });
        search.TextChanged += (_, _) => RefreshTracks(); searchBar.Children.Add(search);
        searchBar.Children.Add(Button("Find in library", () => Work(token => Scan(token, true))));
        search.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) { e.Handled = true; Work(token => Scan(token, true)); } };
        searchBar.Children.Add(count); Row(searchBar, 3);
        site.SelectionChanged += (_, _) => {
            if (resettingSite || cancellation != null) return;
            Work(async _ => { await browser.SelectSiteAsync(site.SelectedIndex == 1 ? MusicSite.Suno : MusicSite.Udio); catalog.Clear(); RefreshTracks(); SetStatus(browser.Site.Name + " selected. Open library / filters, then Scan library."); });
        };
        var destination = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"), ColumnSpacing = 10 };
        destination.Children.Add(new TextBlock { Text = "Save to", VerticalAlignment = VerticalAlignment.Center });
        folder.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Music", "AssetRaider");
        try { if (File.Exists(SettingsPath)) folder.Text = JsonSerializer.Deserialize<string>(File.ReadAllText(SettingsPath)) ?? folder.Text; } catch { }
        Grid.SetColumn(folder, 1); destination.Children.Add(folder);
        var browse = Button("Browse", () => Work(async _ => {
            var chosen = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Save WAV files to", AllowMultiple = false });
            if (chosen.Count > 0 && chosen[0].TryGetLocalPath() is { } path) folder.Text = path;
        })); Grid.SetColumn(browse, 2); destination.Children.Add(browse);
        var open = Button("Open", () => Work(_ => {
            var path = Path.GetFullPath(folder.Text ?? ""); Directory.CreateDirectory(path);
            var start = new ProcessStartInfo(OperatingSystem.IsMacOS() ? "/usr/bin/open" : "xdg-open") { UseShellExecute = false }; start.ArgumentList.Add(path); Process.Start(start)?.Dispose(); return Task.CompletedTask;
        })); Grid.SetColumn(open, 3); destination.Children.Add(open); Row(destination, 7);
        list.ItemsSource = tracks;
        list.Background = Brush.Parse("#192219"); list.BorderBrush = Brush.Parse("#34422E"); list.BorderThickness = new Thickness(1);
        list.ItemTemplate = new FuncDataTemplate<Track>((track, scope) => {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,82,2*,Auto,2*,2*"), ColumnSpacing = 10, Margin = new Thickness(4, 7) };
            var selected = new CheckBox { VerticalAlignment = VerticalAlignment.Center }; selected.Bind(CheckBox.IsCheckedProperty, new Binding(nameof(Track.Selected)) { Source = track, Mode = BindingMode.TwoWay });
            row.Children.Add(selected);
            var play = new Button { Padding = new Thickness(8, 5), HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Center };
            play.Bind(Avalonia.Controls.Button.ContentProperty, new Binding(nameof(Track.PlaybackAction)) { Source = track }); play.Click += (_, _) => _ = PlayTrackAsync(track);
            Grid.SetColumn(play, 1); row.Children.Add(play);
            var name = new TextBlock { Text = track.Title, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(name, 2); row.Children.Add(name);
            var length = new TextBlock { Text = track.Duration, VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(length, 3); row.Children.Add(length);
            var quality = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 12, Foreground = Brush.Parse("#BCD49F"), VerticalAlignment = VerticalAlignment.Center };
            quality.Bind(TextBlock.TextProperty, new Binding(nameof(Track.SourceQualityText)) { Source = track, StringFormat = "Source: {0}" });
            ToolTip.SetTip(quality, "Encoding reported by Chrome. ~kbps is an approximate stream average. Unknown means unavailable; WAV output settings are separate.");
            Grid.SetColumn(quality, 4); row.Children.Add(quality);
            var detail = new TextBlock { TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center }; detail.Bind(TextBlock.TextProperty, new Binding(nameof(Track.Status)) { Source = track }); Grid.SetColumn(detail, 5); row.Children.Add(detail);
            return row;
        }); Row(list, 4);
        var playerBar = new Grid { ColumnDefinitions = new ColumnDefinitions("88,115,*,120"), RowDefinitions = new RowDefinitions("Auto,Auto"), ColumnSpacing = 8, RowSpacing = 5 };
        playerBar.Children.Add(nowPlaying); Grid.SetColumnSpan(nowPlaying, 4);
        playbackToggle.Click += (_, _) => _ = PlayerActionAsync(() => player.ToggleAsync());
        playbackStop.Click += (_, _) => _ = PlayerActionAsync(() => player.StopAsync());
        playbackSeek.AddHandler(Avalonia.Input.InputElement.PointerPressedEvent, (_, _) => seekingPlayer = true, Avalonia.Interactivity.RoutingStrategies.Tunnel, true);
        playbackSeek.AddHandler(Avalonia.Input.InputElement.PointerReleasedEvent, (_, _) => { seekingPlayer = false; _ = PlayerActionAsync(() => player.SeekAsync(playbackSeek.Value / 1000 * (player.State?.Duration ?? 0))); }, Avalonia.Interactivity.RoutingStrategies.Tunnel, true);
        playbackSeek.KeyUp += (_, _) => _ = PlayerActionAsync(() => player.SeekAsync(playbackSeek.Value / 1000 * (player.State?.Duration ?? 0)));
        Control[] controls = [playbackToggle, playbackStop, playbackSeek, playbackTime];
        for (var i = 0; i < controls.Length; i++) { Grid.SetRow(controls[i], 1); Grid.SetColumn(controls[i], i); playerBar.Children.Add(controls[i]); }
        Row(playerBar, 5);
        var spectrum = new StackPanel { Spacing = 3 }; spectrum.Children.Add(new TextBlock { Text = "AUDIO SPECTRUM   ·   50 Hz → 18 kHz", FontSize = 10, Foreground = Brush.Parse("#BCD49F") }); spectrum.Children.Add(new SpectrumView()); Row(spectrum, 6);
        playerTimer.Tick += async (_, _) => await PollPlayerAsync(); playerTimer.Start();
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        var record = Button("Record selected as WAV", () => Work(token => Record(token, false)));
        record.Background = Brush.Parse("#C9F47B"); record.Foreground = Brush.Parse("#1B2811"); record.FontWeight = FontWeight.Bold;
        actions.Children.Add(record);
        actions.Children.Add(Button("Record again (new copy)", () => Work(token => Record(token, true)))); actions.Children.Add(stop);
        actions.Children.Add(new TextBlock { Text = "48 kHz · 16-bit stereo WAV", FontSize = 12, Foreground = Brush.Parse("#BCD49F"), VerticalAlignment = VerticalAlignment.Center });
        stop.Click += (_, _) => { cancellation?.Cancel(); SetStatus(scanControl != null ? "Stopping scan… tracks found will be kept." : "Stopping… unfinished recordings remain partial."); }; Row(actions, 8);
        Row(status, 9); Row(progress, 10); Row(log, 11); Content = root;
        SetStatus(OperatingSystem.IsMacOS() ? "First recording: macOS will request Screen & System Audio Recording permission. Only audio from the dedicated Chrome app is requested. Keep the Mac awake during recording." : "Linux: requires PulseAudio or PipeWire-Pulse and pulseaudio-utils. The dedicated Chrome audio goes to a silent recording channel. Keep the computer awake during recording.");
        UpdateCount(); Closing += OnClosing;
    }
    internal void SeedUiCheck()
    {
        catalog.Add(new Track { Id = "ExampleTrack0001", Title = "Night Drive", Duration = "3:24", SourceQuality = new("MP3", 44100, 96000, 2) });
        catalog.Add(new Track { Id = "ExampleTrack0002", Title = "Sunrise", Duration = "2:48", SourceQuality = SourceQuality.Unknown });
        RefreshTracks();
    }
    private Button Button(string label, Action action)
    {
        var result = new Button { Content = label, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(12, 8), CornerRadius = new CornerRadius(5) };
        result.Click += (_, _) => action(); idle.Add(result); return result;
    }
    private void Select(bool selected) { foreach (var track in tracks) track.Selected = selected; UpdateCount(); }
    private void UpdateCount() => count.Text = $"{tracks.Count(t => t.Selected)} selected / {tracks.Count} shown / {catalog.Count} scanned";
    private void RefreshTracks() { tracks.Clear(); foreach (var track in catalog.Matching(search.Text)) tracks.Add(track); UpdateCount(); }
    private void TogglePause()
    {
        if (scanControl == null) return;
        if (scanControl.PauseRequested) { scanControl.Resume(); pauseScan.Content = "Pause scan"; SetStatus("Resuming scan from the same position…"); }
        else { scanControl.Pause(); pauseScan.Content = "Resume scan"; SetStatus("Pausing after the current browser step…"); }
    }
    private async Task ClearScanAsync()
    {
        if (clearing || playerBusy || (operation is { IsCompleted: false } && scanControl == null)) return;
        clearing = true; clearScan.IsEnabled = false;
        try {
            while (pollingPlayer) await Task.Delay(25);
            cancellation?.Cancel(); if (operation != null) await operation;
            await player.StopAsync(); RefreshPlayer();
            catalog.Clear(); RefreshTracks(); progress.Value = 0;
            SetStatus("Scanned list cleared. Saved WAVs are untouched. Choose filters, then Scan library or Find in library to start again.");
        }
        finally { clearing = false; clearScan.IsEnabled = true; }
    }
    private void SetStatus(string text) { status.Text = text; log.Text = $"{DateTime.Now:HH:mm:ss}  {text}\n" + (log.Text ?? ""); if (log.Text.Length > 20000) log.Text = log.Text[..20000]; }
    private void Work(Func<CancellationToken, Task> action) { if (!closeRequested && !clearing && !playerBusy && operation is not { IsCompleted: false }) operation = RunOperation(action); }
    private async Task RunOperation(Func<CancellationToken, Task> action)
    {
        while (pollingPlayer) await Task.Delay(25);
        cancellation = new(); clearScan.IsEnabled = false; search.IsEnabled = false; foreach (var button in idle) button.IsEnabled = false;
        site.IsEnabled = folder.IsEnabled = list.IsEnabled = false; stop.IsEnabled = true; progress.Value = 0;
        RefreshPlayer();
        try { await player.StopAsync(); await action(cancellation.Token); }
        catch (OperationCanceledException) { SetStatus(scanControl != null ? "Scan stopped. Tracks found are kept; Scan library starts a fresh scan." : "Stopped. Completed WAVs are saved; select remaining tracks to resume."); }
        catch (Exception ex) { SetStatus(ex.Message); }
        finally {
            scanControl = null; pauseScan.IsEnabled = false; pauseScan.Content = "Pause scan"; clearScan.IsEnabled = !clearing; search.IsEnabled = true;
            cancellation.Dispose(); cancellation = null; foreach (var button in idle) button.IsEnabled = true;
            resettingSite = true; site.SelectedIndex = browser.Site.Id == "suno" ? 1 : 0; resettingSite = false;
            site.IsEnabled = folder.IsEnabled = list.IsEnabled = true; stop.IsEnabled = false;
            RefreshPlayer();
        }
    }
    private async Task PlayTrackAsync(Track track)
    {
        if (player.Track?.Key == track.Key) { await PlayerActionAsync(() => player.ToggleAsync()); return; }
        await PlayerActionAsync(() => player.PlayAsync(track, CancellationToken.None));
    }
    private async Task PlayerActionAsync(Func<Task> action)
    {
        if (closeRequested || playerBusy || clearing || operation is { IsCompleted: false }) return;
        playerBusy = true; foreach (var button in idle) button.IsEnabled = false;
        site.IsEnabled = clearScan.IsEnabled = list.IsEnabled = false; RefreshPlayer();
        try { while (pollingPlayer) await Task.Delay(25); await action(); }
        catch (Exception ex) { await player.StopAsync(); SetStatus("Music player: " + ex.Message); }
        finally { playerBusy = false; foreach (var button in idle) button.IsEnabled = true; site.IsEnabled = clearScan.IsEnabled = list.IsEnabled = true; RefreshPlayer(); }
    }
    private async Task PollPlayerAsync()
    {
        if (player.Track == null || playerBusy || pollingPlayer || clearing || operation is { IsCompleted: false }) return;
        pollingPlayer = true;
        try { await player.PollAsync(); RefreshPlayer(); }
        catch (Exception ex) { await player.StopAsync(); RefreshPlayer(); SetStatus("Music player: " + ex.Message); }
        finally { pollingPlayer = false; }
    }
    private void RefreshPlayer()
    {
        var available = !playerBusy && cancellation == null && player.Track != null;
        playbackToggle.IsEnabled = playbackStop.IsEnabled = playbackSeek.IsEnabled = available;
        playbackToggle.Content = player.State?.Done == true ? "Replay" : player.State?.Paused == true ? "Resume" : "Pause";
        nowPlaying.Text = player.Track == null ? "Music player · click Play beside a track · Chrome can stay minimized" : player.Track.Title + " · " + player.Track.SourceQualityText + (player.SpectrumError == null ? "" : " · Spectrum unavailable");
        playbackTime.Text = $"{TimeSpan.FromSeconds(player.State?.Elapsed ?? 0):m\\:ss} / {TimeSpan.FromSeconds(player.State?.Duration ?? 0):m\\:ss}";
        if (!seekingPlayer) playbackSeek.Value = Math.Clamp((player.State?.Elapsed ?? 0) / Math.Max(1, player.State?.Duration ?? 1) * 1000, 0, 1000);
    }
    private async Task Scan(CancellationToken token, bool find = false)
    {
        scanControl = new(); pauseScan.IsEnabled = true; clearScan.IsEnabled = true; search.IsEnabled = true;
        scanControl.Paused = () => SetStatus("Scan paused. Resume continues from here. Clear scanned list starts over; change site filters between scans.");
        catalog.Clear(); RefreshTracks();
        var query = find ? search.Text?.Trim() ?? "" : null;
        SetStatus(find && browser.Site.Id == "suno" ? "Searching Suno's library for the entered name…" : "Scanning the current filtered library. Search names as tracks arrive; Pause scan keeps your position.");
        SetStatus(await browser.ScanAsync(fresh => {
            foreach (var track in fresh) if (catalog.Add(track)) track.PropertyChanged += (_, _) => UpdateCount();
            RefreshTracks(); status.Text = $"Scanning… {catalog.Count} tracks found, {tracks.Count} match the name filter";
        }, scanControl, token, query));
    }
    private async Task Record(CancellationToken token, bool recordAgain)
    {
        var selected = tracks.Where(t => t.Selected).ToArray();
        if (selected.Length == 0) { SetStatus("Choose at least one track, or Select all."); return; }
        Directory.CreateDirectory(MusicBrowser.AppData); File.WriteAllText(SettingsPath, JsonSerializer.Serialize(folder.Text));
        if (recordAgain) SetStatus("Recording fresh copies of every selected track. Existing WAVs will be kept.");
        SetStatus($"Recording {selected.Length} tracks. Keep the dedicated Chrome window open and the computer awake.");
        await new RecorderQueue(browser).RunAsync(selected, folder.Text ?? "", (track, message, fraction) => {
            track.Status = message; progress.Value = fraction * 100; status.Text = track.Title + " — " + message;
            if (message.StartsWith("Saved") || message.StartsWith("Failed") || message.StartsWith("Already")) SetStatus(status.Text + " | Source: " + track.SourceQualityText);
        }, token, recordAgain);
        var saved = selected.Count(t => t.Status == "Saved WAV");
        var skipped = selected.Count(t => t.Status.StartsWith("Already saved:"));
        SetStatus($"Queue finished: {saved} newly recorded, {skipped} skipped (existing WAV), {selected.Length - saved - skipped} need attention.");
    }
    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (closing) return; e.Cancel = true; if (closeRequested) return; closeRequested = true;
        playerTimer.Stop();
        cancellation?.Cancel(); if (operation != null) await operation;
        while (playerBusy || pollingPlayer) await Task.Delay(50);
        await player.StopAsync();
        await browser.DisposeAsync(); closing = true; Close();
    }
}

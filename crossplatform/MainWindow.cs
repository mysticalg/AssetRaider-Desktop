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

namespace AssetRaider.Desktop;

public sealed class MainWindow : Window
{
    private readonly MusicBrowser browser = new();
    private readonly ObservableCollection<Track> tracks = [];
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
        Title = "AssetRaider 0.4.0 beta — Udio + Suno to WAV"; Width = 1080; Height = 800; MinWidth = 850; MinHeight = 650;
        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,*,Auto,Auto,Auto,Auto"), Margin = new Thickness(24), RowSpacing = 12 };
        void Row(Control control, int row) { Grid.SetRow(control, row); root.Children.Add(control); }
        Row(new TextBlock { Text = "AssetRaider / Udio + Suno to WAV", FontSize = 26, FontWeight = FontWeight.Bold }, 0);
        Row(new TextBlock { Text = "1. Sign in with normal Chrome, then close that window → 2. Load library → 3. Select and record\n48 kHz / 16-bit stereo WAV · Playback speed · About 11 MB per minute", TextWrapping = TextWrapping.Wrap }, 1);
        var toolbar = new WrapPanel { Orientation = Orientation.Horizontal };
        toolbar.Children.Add(site);
        toolbar.Children.Add(Button("Sign in (normal Chrome)", () => Work(async _ => { await browser.OpenLoginAsync(); SetStatus("Sign in in the separate Chrome window, then close it and click Load library."); })));
        toolbar.Children.Add(Button("Load library", () => Work(Scan)));
        toolbar.Children.Add(Button("Select all", () => Select(true)));
        toolbar.Children.Add(Button("Select none", () => Select(false)));
        toolbar.Children.Add(count); Row(toolbar, 2);
        site.SelectionChanged += (_, _) => {
            if (resettingSite || cancellation != null) return;
            Work(async _ => { await browser.SelectSiteAsync(site.SelectedIndex == 1 ? MusicSite.Suno : MusicSite.Udio); tracks.Clear(); UpdateCount(); SetStatus(browser.Site.Name + " selected."); });
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
        })); Grid.SetColumn(open, 3); destination.Children.Add(open); Row(destination, 3);
        list.ItemsSource = tracks;
        list.ItemTemplate = new FuncDataTemplate<Track>((track, _) => {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,2*,Auto,3*"), ColumnSpacing = 14, Margin = new Thickness(4, 7) };
            var selected = new CheckBox { VerticalAlignment = VerticalAlignment.Center }; selected.Bind(CheckBox.IsCheckedProperty, new Binding(nameof(Track.Selected)) { Source = track, Mode = BindingMode.TwoWay });
            row.Children.Add(selected);
            var name = new TextBlock { Text = track.Title, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(name, 1); row.Children.Add(name);
            var length = new TextBlock { Text = track.Duration, VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(length, 2); row.Children.Add(length);
            var detail = new TextBlock { TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center }; detail.Bind(TextBlock.TextProperty, new Binding(nameof(Track.Status)) { Source = track }); Grid.SetColumn(detail, 3); row.Children.Add(detail);
            return row;
        }); Row(list, 4);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        actions.Children.Add(Button("Record selected as WAV", () => Work(Record))); actions.Children.Add(stop);
        stop.Click += (_, _) => { cancellation?.Cancel(); SetStatus("Stopping… unfinished recordings remain partial."); }; Row(actions, 5);
        Row(status, 6); Row(progress, 7); Row(log, 8); Content = root;
        SetStatus(OperatingSystem.IsMacOS() ? "First recording: macOS will request Screen & System Audio Recording permission. Only audio from the dedicated Chrome app is requested. Keep the Mac awake during recording." : "Linux: requires PulseAudio or PipeWire-Pulse and pulseaudio-utils. The dedicated Chrome audio goes to a silent recording channel. Keep the computer awake during recording.");
        UpdateCount(); Closing += OnClosing;
    }
    private Button Button(string label, Action action)
    {
        var result = new Button { Content = label, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(12, 8) };
        result.Click += (_, _) => action(); idle.Add(result); return result;
    }
    private void Select(bool selected) { foreach (var track in tracks) track.Selected = selected; UpdateCount(); }
    private void UpdateCount() => count.Text = $"{tracks.Count(t => t.Selected)} selected / {tracks.Count}";
    private void SetStatus(string text) { status.Text = text; log.Text = $"{DateTime.Now:HH:mm:ss}  {text}\n" + (log.Text ?? ""); if (log.Text.Length > 20000) log.Text = log.Text[..20000]; }
    private void Work(Func<CancellationToken, Task> action) { if (operation is not { IsCompleted: false }) operation = RunOperation(action); }
    private async Task RunOperation(Func<CancellationToken, Task> action)
    {
        cancellation = new(); foreach (var button in idle) button.IsEnabled = false;
        site.IsEnabled = folder.IsEnabled = list.IsEnabled = false; stop.IsEnabled = true; progress.Value = 0;
        try { await action(cancellation.Token); }
        catch (OperationCanceledException) { SetStatus("Stopped. Completed WAVs are saved; select remaining tracks to resume."); }
        catch (Exception ex) { SetStatus(ex.Message); }
        finally {
            cancellation.Dispose(); cancellation = null; foreach (var button in idle) button.IsEnabled = true;
            resettingSite = true; site.SelectedIndex = browser.Site.Id == "suno" ? 1 : 0; resettingSite = false;
            site.IsEnabled = folder.IsEnabled = list.IsEnabled = true; stop.IsEnabled = false;
        }
    }
    private async Task Scan(CancellationToken token)
    {
        SetStatus("Loading " + browser.Site.Name + " library/workspace… Stop keeps tracks already found.");
        var selections = tracks.Where(t => t.Selected).Select(t => t.Key).ToHashSet(); var cleared = false;
        SetStatus(await browser.ScanAsync(fresh => {
            if (!cleared) { tracks.Clear(); cleared = true; }
            foreach (var track in fresh) { track.Selected = selections.Contains(track.Key); track.PropertyChanged += (_, _) => UpdateCount(); tracks.Add(track); }
            UpdateCount(); status.Text = $"Loading… {tracks.Count} tracks found";
        }, token));
    }
    private async Task Record(CancellationToken token)
    {
        var selected = tracks.Where(t => t.Selected).ToArray();
        if (selected.Length == 0) { SetStatus("Choose at least one track, or Select all."); return; }
        Directory.CreateDirectory(MusicBrowser.AppData); File.WriteAllText(SettingsPath, JsonSerializer.Serialize(folder.Text));
        SetStatus($"Recording {selected.Length} tracks. Keep the dedicated Chrome window open and the computer awake.");
        await new RecorderQueue(browser).RunAsync(selected, folder.Text ?? "", (track, message, fraction) => {
            track.Status = message; progress.Value = fraction * 100; status.Text = track.Title + " — " + message;
            if (message.StartsWith("Saved") || message.StartsWith("Failed") || message.StartsWith("Already")) SetStatus(status.Text);
        }, token);
        var saved = selected.Count(t => t.Status is "Saved WAV" or "Already saved"); SetStatus($"Queue finished: {saved}/{selected.Length} saved or already complete; {selected.Length - saved} need attention.");
    }
    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (closing) return; e.Cancel = true; if (closeRequested) return; closeRequested = true;
        cancellation?.Cancel(); if (operation != null) await operation;
        await browser.DisposeAsync(); closing = true; Close();
    }
}

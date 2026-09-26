using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;

namespace AssetRaider.Desktop;

public sealed class MainForm : Form
{
    private readonly MusicBrowser browser = new();
    private readonly ComboBox sitePicker = new();
    private readonly BindingList<Track> tracks = [];
    private readonly DataGridView grid = new();
    private readonly TextBox folder = new();
    private readonly Label status = new();
    private readonly Label count = new();
    private readonly ProgressBar progress = new();
    private readonly TextBox log = new();
    private readonly List<Button> idleButtons = [];
    private readonly Button stop = new();
    private CancellationTokenSource? cancellation;
    private Task? operation;
    private bool closing;
    private bool closeRequested;
    private string SettingsPath => Path.Combine(MusicBrowser.AppData, "settings.json");

    public MainForm()
    {
        Text = "AssetRaider 0.3.0 — Udio + Suno WAV Recorder";
        MinimumSize = new Size(820, 600); Size = new Size(1100, 790);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10);
        BackColor = Color.FromArgb(245, 247, 251);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22), ColumnCount = 1, RowCount = 10 };
        layout.RowStyles.Add(new(SizeType.Absolute, 50));
        layout.RowStyles.Add(new(SizeType.Absolute, 48));
        layout.RowStyles.Add(new(SizeType.Absolute, 40));
        layout.RowStyles.Add(new(SizeType.Absolute, 42));
        layout.RowStyles.Add(new(SizeType.Absolute, 44));
        layout.RowStyles.Add(new(SizeType.Percent, 100));
        layout.RowStyles.Add(new(SizeType.Absolute, 50));
        layout.RowStyles.Add(new(SizeType.Absolute, 58));
        layout.RowStyles.Add(new(SizeType.Absolute, 14));
        layout.RowStyles.Add(new(SizeType.Absolute, 105));
        Controls.Add(layout);
        var heading = new Label { Text = "AssetRaider  /  Udio + Suno to WAV", Font = new Font("Segoe UI", 21, FontStyle.Bold), Dock = DockStyle.Fill };
        layout.Controls.Add(heading);
        layout.Controls.Add(new Label { Text = "1. Sign in using normal Chrome, then close that window   →   2. Load library   →   3. Select and record\nStereo WAV · 48 kHz / 16-bit · Normal playback speed · About 11 MB per minute", Dock = DockStyle.Fill });
        var siteBar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        siteBar.Controls.Add(new Label { Text = "Music site", AutoSize = true, Margin = new Padding(0, 7, 10, 0) });
        sitePicker.DropDownStyle = ComboBoxStyle.DropDownList; sitePicker.Width = 135;
        sitePicker.Items.AddRange(["Udio", "Suno"]); sitePicker.SelectedIndex = 0;
        sitePicker.SelectionChangeCommitted += (_, _) => Work(async _ => {
            await browser.SelectSiteAsync(sitePicker.SelectedIndex == 1 ? MusicSite.Suno : MusicSite.Udio);
            tracks.Clear(); UpdateCount();
            SetStatus($"{browser.Site.Name} selected. Sign in once, then Load library. Suno scans the selected workspace; Udio scans the current library view.");
        });
        siteBar.Controls.Add(sitePicker);
        siteBar.Controls.Add(new Label { Text = "Select all applies to tracks loaded from this site and workspace.", AutoSize = true, Margin = new Padding(14, 7, 0, 0) });
        layout.Controls.Add(siteBar);
        var connect = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        connect.Controls.Add(MakeButton("1  Sign in (normal Chrome)", () => Work(async _ => { await browser.OpenLoginAsync(); SetStatus($"Sign into {browser.Site.Name} in the normal Chrome window. Once your songs are visible, CLOSE that separate Chrome window, then click Load library. Login runs without automation."); })));
        connect.Controls.Add(MakeButton("2  Load library", () => Work(Scan)));
        connect.Controls.Add(MakeButton("Select all", () => SelectTracks(true)));
        connect.Controls.Add(MakeButton("Select none", () => SelectTracks(false)));
        count.AutoSize = true; count.Margin = new Padding(12, 10, 0, 0); connect.Controls.Add(count);
        layout.Controls.Add(connect);
        var destination = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4 };
        destination.ColumnStyles.Add(new(SizeType.Absolute, 68)); destination.ColumnStyles.Add(new(SizeType.Percent, 100));
        destination.ColumnStyles.Add(new(SizeType.Absolute, 94)); destination.ColumnStyles.Add(new(SizeType.Absolute, 100));
        destination.Controls.Add(new Label { Text = "Save to", AutoSize = true, Margin = new Padding(0, 9, 0, 0) });
        folder.Dock = DockStyle.Fill;
        folder.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "AssetRaider");
        try { if (File.Exists(SettingsPath)) folder.Text = JsonSerializer.Deserialize<string>(File.ReadAllText(SettingsPath)) ?? folder.Text; } catch { }
        destination.Controls.Add(folder);
        destination.Controls.Add(MakeButton("Browse…", () => { using var dialog = new FolderBrowserDialog { InitialDirectory = folder.Text }; if (dialog.ShowDialog(this) == DialogResult.OK) folder.Text = dialog.SelectedPath; }));
        destination.Controls.Add(MakeButton("Open folder", () => { try { Directory.CreateDirectory(folder.Text); Process.Start(new ProcessStartInfo(folder.Text) { UseShellExecute = true }); } catch (Exception e) { SetStatus(e.Message); } }));
        layout.Controls.Add(destination);
        grid.Dock = DockStyle.Fill; grid.AutoGenerateColumns = false; grid.AllowUserToAddRows = false; grid.AllowUserToDeleteRows = false;
        grid.RowHeadersVisible = false; grid.BackgroundColor = Color.White; grid.BorderStyle = BorderStyle.None;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect; grid.MultiSelect = false;
        grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells; grid.RowTemplate.Height = 34;
        grid.EnableHeadersVisualStyles = false; grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(226, 232, 242);
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 253);
        grid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = nameof(Track.Selected), HeaderText = "✓", Width = 44 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(Track.Title), HeaderText = "Song", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 45, ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(Track.Duration), HeaderText = "Length", Width = 70, ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(Track.Status), HeaderText = "Status", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 55, ReadOnly = true });
        grid.DataSource = tracks;
        grid.CurrentCellDirtyStateChanged += (_, _) => { if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
        grid.CellValueChanged += (_, _) => UpdateCount();
        layout.Controls.Add(grid);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 7, 0, 0) };
        var record = MakeButton("3  Record selected as WAV", () => Work(Record));
        record.BackColor = Color.FromArgb(47, 75, 190); record.ForeColor = Color.White; actions.Controls.Add(record);
        stop.Text = "Stop"; stop.AutoSize = true; stop.Height = 34; stop.Enabled = false;
        stop.Click += (_, _) => { cancellation?.Cancel(); SetStatus("Stopping… any unfinished recording will be kept as a partial WAV."); };
        actions.Controls.Add(stop);
        layout.Controls.Add(actions);
        status.Dock = DockStyle.Fill; status.AutoEllipsis = true; status.Text = "First time: Sign in using normal Chrome, then close that window and Load library. Already signed in: go straight to Load library.";
        layout.Controls.Add(status);
        progress.Dock = DockStyle.Fill; layout.Controls.Add(progress);
        log.Dock = DockStyle.Fill; log.Multiline = true; log.ReadOnly = true; log.ScrollBars = ScrollBars.Vertical;
        log.BackColor = Color.White; log.BorderStyle = BorderStyle.FixedSingle; layout.Controls.Add(log);
        UpdateCount();
        FormClosing += OnClosing;
    }

    private Button MakeButton(string text, Action action)
    {
        var button = new Button { Text = text, AutoSize = true, Height = 34, FlatStyle = FlatStyle.Flat, Padding = new Padding(7, 3, 7, 3), BackColor = Color.White };
        button.FlatAppearance.BorderColor = Color.FromArgb(210, 216, 228);
        button.Click += (_, _) => action(); idleButtons.Add(button); return button;
    }
    private void UpdateCount() => count.Text = $"{tracks.Count(t => t.Selected)} selected / {tracks.Count}";
    private void SelectTracks(bool selected) { grid.EndEdit(); foreach (var track in tracks) track.Selected = selected; tracks.ResetBindings(); UpdateCount(); }
    private void SetStatus(string text) { status.Text = text; log.AppendText($"{DateTime.Now:HH:mm:ss}  {text}{Environment.NewLine}"); }

    private void Work(Func<CancellationToken, Task> action)
    {
        if (operation is { IsCompleted: false }) return;
        operation = RunOperation(action);
    }
    private async Task RunOperation(Func<CancellationToken, Task> action)
    {
        cancellation = new();
        foreach (var button in idleButtons) button.Enabled = false;
        sitePicker.Enabled = false; folder.Enabled = false; grid.ReadOnly = true; stop.Enabled = true; progress.Value = 0;
        try { await action(cancellation.Token); }
        catch (OperationCanceledException) { SetStatus("Stopped. Completed WAVs are saved; select remaining songs and record to resume."); }
        catch (Exception ex) { SetStatus(ex.Message); }
        finally
        {
            stop.Enabled = false; grid.ReadOnly = false; folder.Enabled = true;
            sitePicker.SelectedIndex = browser.Site.Id == "suno" ? 1 : 0; sitePicker.Enabled = true;
            foreach (DataGridViewColumn column in grid.Columns) column.ReadOnly = column.Index != 0;
            foreach (var button in idleButtons) button.Enabled = true;
            cancellation.Dispose(); cancellation = null;
        }
    }
    private async Task Scan(CancellationToken token)
    {
        SetStatus($"Loading the current {browser.Site.Name} library/workspace. You can stop the scan and select tracks already found.");
        var oldSelections = tracks.Where(t => t.Selected).Select(t => t.Key).ToHashSet();
        var scanned = false;
        var result = await browser.ScanAsync(fresh => {
            if (!scanned) { tracks.Clear(); scanned = true; }
            foreach (var track in fresh) { track.Selected = oldSelections.Contains(track.Key); tracks.Add(track); }
            UpdateCount(); status.Text = $"Loading library… {tracks.Count} tracks found";
        }, token);
        SetStatus(result);
    }
    private async Task Record(CancellationToken token)
    {
        grid.EndEdit();
        var selected = tracks.Where(t => t.Selected).ToArray();
        if (selected.Length == 0) { SetStatus("Select one or more songs first, or click Select all."); return; }
        Directory.CreateDirectory(MusicBrowser.AppData);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(folder.Text));
        SetStatus($"Recording {selected.Length} selected songs. Keep the app’s Chrome window open; avoid other playback in that window. You can use your usual browser separately.");
        await new RecorderQueue(browser).RunAsync(selected, folder.Text, (track, message, fraction) => {
            var terminal = message.StartsWith("Saved") || message.StartsWith("Failed") || message.StartsWith("Already") || message.StartsWith("Stopped");
            track.Status = message; tracks.ResetItem(tracks.IndexOf(track));
            progress.Value = (int)(fraction * 100); status.Text = $"{track.Title} — {message}";
            if (terminal) log.AppendText($"{DateTime.Now:HH:mm:ss}  {track.Title}: {message}{Environment.NewLine}");
        }, token);
        var saved = selected.Count(t => t.Status is "Saved WAV" or "Already saved");
        SetStatus($"Queue finished: {saved}/{selected.Length} saved or already complete. {selected.Length - saved} need attention. See each track’s status.");
    }
    private async void OnClosing(object? sender, FormClosingEventArgs e)
    {
        if (closing) return;
        e.Cancel = true;
        if (closeRequested) return;
        closeRequested = true;
        cancellation?.Cancel();
        if (operation != null) await operation;
        await browser.DisposeAsync();
        closing = true; Close();
    }
}

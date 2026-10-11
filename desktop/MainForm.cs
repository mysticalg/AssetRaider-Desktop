using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;

namespace AssetRaider.Desktop;

public sealed class MainForm : Form
{
    private readonly MusicBrowser browser = new();
    private readonly ComboBox sitePicker = new();
    private readonly BindingList<Track> tracks = [];
    private readonly LibraryCatalog catalog = new();
    private readonly TextBox search = new() { Width = 260, PlaceholderText = "Type a track name…" };
    private readonly Button pauseScan = new() { Text = "Pause scan", AutoSize = true, Height = 34, Enabled = false };
    private Button clearScan = null!;
    private ScanControl? scanControl;
    private bool clearing;
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
        Text = "AssetRaider 0.3.4 — Udio + Suno WAV Recorder";
        MinimumSize = new Size(960, 760); Size = new Size(1160, 880);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10);
        BackColor = Color.FromArgb(242, 245, 239); ForeColor = Color.FromArgb(31, 40, 32);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 1, RowCount = 10 };
        layout.ColumnStyles.Add(new(SizeType.Percent, 100));
        layout.RowStyles.Add(new(SizeType.Absolute, 92));
        layout.RowStyles.Add(new(SizeType.Absolute, 46));
        layout.RowStyles.Add(new(SizeType.Absolute, 46));
        layout.RowStyles.Add(new(SizeType.Absolute, 46));
        layout.RowStyles.Add(new(SizeType.Percent, 100));
        layout.RowStyles.Add(new(SizeType.Absolute, 46));
        layout.RowStyles.Add(new(SizeType.Absolute, 52));
        layout.RowStyles.Add(new(SizeType.Absolute, 48));
        layout.RowStyles.Add(new(SizeType.Absolute, 10));
        layout.RowStyles.Add(new(SizeType.Absolute, 80));
        Controls.Add(layout);
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(12, 8, 12, 8), BackColor = Color.FromArgb(24, 31, 25), Margin = new Padding(0, 0, 0, 12) };
        header.ColumnStyles.Add(new(SizeType.Absolute, 68)); header.ColumnStyles.Add(new(SizeType.Percent, 100));
        header.RowCount = 1; header.RowStyles.Add(new(SizeType.Percent, 100));
        using var logoStream = typeof(MainForm).Assembly.GetManifestResourceStream("AssetRaider.Brand.Icon.png")!;
        using var sourceLogo = Image.FromStream(logoStream);
        var logo = new PictureBox { Image = new Bitmap(sourceLogo), SizeMode = PictureBoxSizeMode.Zoom, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 12, 0), AccessibleName = "AssetRaider logo" };
        logo.Disposed += (_, _) => logo.Image?.Dispose(); header.Controls.Add(logo);
        using var iconStream = typeof(MainForm).Assembly.GetManifestResourceStream("AssetRaider.Brand.Icon.ico")!;
        Icon = new Icon(iconStream);
        var title = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, Margin = Padding.Empty };
        title.RowStyles.Add(new(SizeType.Absolute, 38)); title.RowStyles.Add(new(SizeType.Percent, 100));
        title.Controls.Add(new Label { Text = "AssetRaider", Font = new Font("Segoe UI", 20, FontStyle.Bold), ForeColor = Color.FromArgb(235, 243, 223), Dock = DockStyle.Fill, Margin = Padding.Empty });
        title.Controls.Add(new Label { Text = "YOUR MUSIC, SAVED LOCALLY   /   Udio + Suno playback to WAV", Font = new Font("Segoe UI", 9), ForeColor = Color.FromArgb(191, 211, 171), Dock = DockStyle.Fill, Margin = Padding.Empty });
        header.Controls.Add(title); layout.Controls.Add(header, 0, 0);
        var siteBar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        siteBar.Controls.Add(new Label { Text = "Music site", AutoSize = true, Margin = new Padding(0, 7, 10, 0) });
        sitePicker.DropDownStyle = ComboBoxStyle.DropDownList; sitePicker.Width = 135;
        sitePicker.Items.AddRange(["Udio", "Suno"]); sitePicker.SelectedIndex = 0;
        sitePicker.SelectionChangeCommitted += (_, _) => Work(async _ => {
            await browser.SelectSiteAsync(sitePicker.SelectedIndex == 1 ? MusicSite.Suno : MusicSite.Udio);
            catalog.Clear(); RefreshTracks();
            SetStatus($"{browser.Site.Name} selected. Sign in once, then Scan library. Open library / filters to choose your view, then Scan library.");
        });
        siteBar.Controls.Add(sitePicker);
        siteBar.Controls.Add(MakeButton("Sign in (normal Chrome)", () => Work(async _ => { await browser.OpenLoginAsync(); SetStatus($"Sign into {browser.Site.Name} in the normal Chrome window. Once your songs are visible, CLOSE that separate Chrome window, then click Scan library. Login runs without automation."); })));
        siteBar.Controls.Add(MakeButton("Open library / filters", () => Work(async _ => { await browser.OpenLibraryAsync(); SetStatus("Choose filters in Chrome, then click Scan library. Stop or clear an existing scan before changing site filters."); })));
        layout.Controls.Add(siteBar, 0, 1);
        var scanBar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        scanBar.Controls.Add(MakeButton("Scan library", () => Work(token => Scan(token))));
        StyleButton(pauseScan); pauseScan.Click += (_, _) => TogglePause(); scanBar.Controls.Add(pauseScan);
        clearScan = MakeButton("Clear scanned list", async () => await ClearScanAsync()); idleButtons.Remove(clearScan); scanBar.Controls.Add(clearScan);
        scanBar.Controls.Add(MakeButton("Select all", () => SelectTracks(true)));
        scanBar.Controls.Add(MakeButton("Select none", () => SelectTracks(false)));
        layout.Controls.Add(scanBar, 0, 2);
        var searchBar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        searchBar.Controls.Add(new Label { Text = "Track name", AutoSize = true, Margin = new Padding(0, 7, 8, 0) });
        search.TextChanged += (_, _) => RefreshTracks(); searchBar.Controls.Add(search);
        searchBar.Controls.Add(MakeButton("Find in library", () => Work(token => Scan(token, true))));
        search.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Work(token => Scan(token, true)); } };
        count.AutoSize = true; count.Margin = new Padding(12, 7, 0, 0); searchBar.Controls.Add(count);
        layout.Controls.Add(searchBar, 0, 3);
        var destination = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4 };
        destination.ColumnStyles.Add(new(SizeType.Absolute, 68)); destination.ColumnStyles.Add(new(SizeType.Percent, 100));
        destination.ColumnStyles.Add(new(SizeType.Absolute, 110)); destination.ColumnStyles.Add(new(SizeType.Absolute, 125));
        destination.Controls.Add(new Label { Text = "Save to", AutoSize = true, Margin = new Padding(0, 9, 0, 0) });
        folder.Dock = DockStyle.Fill;
        folder.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "AssetRaider");
        try { if (File.Exists(SettingsPath)) folder.Text = JsonSerializer.Deserialize<string>(File.ReadAllText(SettingsPath)) ?? folder.Text; } catch { }
        destination.Controls.Add(folder);
        destination.Controls.Add(MakeButton("Browse…", () => { using var dialog = new FolderBrowserDialog { InitialDirectory = folder.Text }; if (dialog.ShowDialog(this) == DialogResult.OK) folder.Text = dialog.SelectedPath; }));
        destination.Controls.Add(MakeButton("Open", () => { try { Directory.CreateDirectory(folder.Text); Process.Start(new ProcessStartInfo(folder.Text) { UseShellExecute = true }); } catch (Exception e) { SetStatus(e.Message); } }));
        layout.Controls.Add(destination, 0, 5);
        grid.Dock = DockStyle.Fill; grid.AutoGenerateColumns = false; grid.AllowUserToAddRows = false; grid.AllowUserToDeleteRows = false;
        grid.RowHeadersVisible = false; grid.BackgroundColor = Color.White; grid.BorderStyle = BorderStyle.None;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect; grid.MultiSelect = false;
        grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells; grid.RowTemplate.Height = 34;
        grid.EnableHeadersVisualStyles = false; grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(222, 231, 216);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = ForeColor; grid.ColumnHeadersHeight = 36;
        grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(5, 0, 5, 0);
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = grid.ColumnHeadersDefaultCellStyle.BackColor;
        grid.DefaultCellStyle.Padding = new Padding(5, 7, 5, 7);
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(220, 240, 194); grid.DefaultCellStyle.SelectionForeColor = ForeColor;
        grid.GridColor = Color.FromArgb(230, 236, 225); grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(249, 251, 246);
        grid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = nameof(Track.Selected), HeaderText = "✓", Width = 44 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(Track.Title), HeaderText = "Song", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 45, ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(Track.Duration), HeaderText = "Length", Width = 70, ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(Track.Status), HeaderText = "Status", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 55, ReadOnly = true });
        grid.DataSource = tracks;
        grid.CurrentCellDirtyStateChanged += (_, _) => { if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
        grid.CellValueChanged += (_, _) => UpdateCount();
        layout.Controls.Add(grid, 0, 4);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 7, 0, 0) };
        var record = MakeButton("Record selected as WAV", () => Work(token => Record(token, false)));
        record.BackColor = Color.FromArgb(201, 244, 123); record.ForeColor = Color.FromArgb(27, 40, 17); record.Font = new Font(Font, FontStyle.Bold); actions.Controls.Add(record);
        actions.Controls.Add(MakeButton("Record again (new copy)", () => Work(token => Record(token, true))));
        stop.Text = "Stop"; StyleButton(stop); stop.Enabled = false;
        stop.Click += (_, _) => { cancellation?.Cancel(); SetStatus(scanControl != null ? "Stopping scan… tracks found will be kept." : "Stopping… any unfinished recording will be kept as a partial WAV."); };
        actions.Controls.Add(stop);
        actions.Controls.Add(new Label { Text = "48 kHz · 16-bit stereo WAV", AutoSize = true, ForeColor = Color.FromArgb(80, 99, 67), Margin = new Padding(8, 10, 0, 0), Font = new Font("Segoe UI", 9) });
        layout.Controls.Add(actions, 0, 6);
        status.Dock = DockStyle.Fill; status.AutoEllipsis = true; status.Text = "Sign in once, then Open library / filters and Scan library. Type a name to filter scanned tracks; Find in library searches Suno directly.";
        layout.Controls.Add(status, 0, 7);
        progress.Dock = DockStyle.Fill; layout.Controls.Add(progress, 0, 8);
        log.Dock = DockStyle.Fill; log.Multiline = true; log.ReadOnly = true; log.ScrollBars = ScrollBars.Vertical;
        log.BackColor = Color.FromArgb(233, 239, 227); log.ForeColor = Color.FromArgb(64, 77, 56); log.Font = new Font("Consolas", 9); log.BorderStyle = BorderStyle.None; layout.Controls.Add(log, 0, 9);
        var tips = new ToolTip(); tips.SetToolTip(record, "Record visible selected tracks. Output: 48 kHz / 16-bit stereo WAV, about 11 MB per minute. Source quality is unchanged.");
        tips.SetToolTip(search, "Filter scanned tracks by name. Find in library searches Suno directly. Select all applies to visible matches.");
        Disposed += (_, _) => tips.Dispose();
        foreach (var bar in layout.Controls.OfType<FlowLayoutPanel>()) bar.Margin = Padding.Empty;
        UpdateCount();
        FormClosing += OnClosing;
    }

    private Button MakeButton(string text, Action action)
    {
        var button = new Button { Text = text }; StyleButton(button);
        button.Click += (_, _) => action(); idleButtons.Add(button); return button;
    }
    private static void StyleButton(Button button)
    {
        button.AutoSize = true; button.Height = 34; button.FlatStyle = FlatStyle.Flat;
        button.Padding = new Padding(7, 2, 7, 2); button.BackColor = Color.White;
        button.Margin = new Padding(0, 2, 8, 2); button.FlatAppearance.BorderColor = Color.FromArgb(200, 213, 190);
    }
    private void UpdateCount() => count.Text = $"{tracks.Count(t => t.Selected)} selected / {tracks.Count} shown / {catalog.Count} scanned";
    private void RefreshTracks()
    {
        grid.EndEdit(); tracks.RaiseListChangedEvents = false;
        tracks.Clear(); foreach (var track in catalog.Matching(search.Text)) tracks.Add(track);
        tracks.RaiseListChangedEvents = true; tracks.ResetBindings(); UpdateCount();
    }
    private void TogglePause()
    {
        if (scanControl == null) return;
        if (scanControl.PauseRequested) { scanControl.Resume(); pauseScan.Text = "Pause scan"; SetStatus("Resuming scan from the same position…"); }
        else { scanControl.Pause(); pauseScan.Text = "Resume scan"; SetStatus("Pausing after the current browser step…"); }
    }
    private async Task ClearScanAsync()
    {
        if (clearing || (operation is { IsCompleted: false } && scanControl == null)) return;
        clearing = true; clearScan.Enabled = false;
        try {
            cancellation?.Cancel(); if (operation != null) await operation;
            catalog.Clear(); RefreshTracks(); progress.Value = 0;
            SetStatus("Scanned list cleared. Saved WAVs are untouched. Choose filters, then Scan library or Find in library to start again.");
        }
        finally { clearing = false; clearScan.Enabled = true; }
    }
    private void SelectTracks(bool selected) { grid.EndEdit(); foreach (var track in tracks) track.Selected = selected; tracks.ResetBindings(); UpdateCount(); }
    private void SetStatus(string text) { status.Text = text; log.AppendText($"{DateTime.Now:HH:mm:ss}  {text}{Environment.NewLine}"); }

    private void Work(Func<CancellationToken, Task> action)
    {
        if (clearing || operation is { IsCompleted: false }) return;
        operation = RunOperation(action);
    }
    private async Task RunOperation(Func<CancellationToken, Task> action)
    {
        cancellation = new(); clearScan.Enabled = false; search.Enabled = false;
        foreach (var button in idleButtons) button.Enabled = false;
        sitePicker.Enabled = false; folder.Enabled = false; grid.ReadOnly = true; stop.Enabled = true; progress.Value = 0;
        try { await action(cancellation.Token); }
        catch (OperationCanceledException) { SetStatus(scanControl != null ? "Scan stopped. Tracks found are kept; Scan library starts a fresh scan." : "Stopped. Completed WAVs are saved; select remaining songs and record to resume."); }
        catch (Exception ex) { SetStatus(ex.Message); }
        finally
        {
            scanControl = null; pauseScan.Enabled = false; pauseScan.Text = "Pause scan"; clearScan.Enabled = !clearing; search.Enabled = true;
            stop.Enabled = false; grid.ReadOnly = false; folder.Enabled = true;
            sitePicker.SelectedIndex = browser.Site.Id == "suno" ? 1 : 0; sitePicker.Enabled = true;
            foreach (DataGridViewColumn column in grid.Columns) column.ReadOnly = column.Index != 0;
            foreach (var button in idleButtons) button.Enabled = true;
            cancellation.Dispose(); cancellation = null;
        }
    }
    private async Task Scan(CancellationToken token, bool find = false)
    {
        scanControl = new(); pauseScan.Enabled = true; clearScan.Enabled = true; search.Enabled = true;
        scanControl.Paused = () => SetStatus("Scan paused. Resume scan continues from here. Clear scanned list starts over; change site filters between scans.");
        catalog.Clear(); RefreshTracks();
        var query = find ? search.Text.Trim() : null;
        SetStatus(find && browser.Site.Id == "suno" ? "Searching Suno's library for the entered name…" : "Scanning the current filtered library. Search names as tracks arrive; Pause scan keeps your position.");
        var result = await browser.ScanAsync(fresh => {
            foreach (var track in fresh) catalog.Add(track);
            RefreshTracks(); status.Text = $"Scanning… {catalog.Count} tracks found, {tracks.Count} match the name filter";
        }, scanControl, token, query);
        SetStatus(result);
    }
    private async Task Record(CancellationToken token, bool recordAgain)
    {
        grid.EndEdit();
        var selected = tracks.Where(t => t.Selected).ToArray();
        if (selected.Length == 0) { SetStatus("Select one or more songs first, or click Select all."); return; }
        Directory.CreateDirectory(MusicBrowser.AppData);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(folder.Text));
        if (recordAgain) SetStatus("Recording fresh copies of every selected track. Existing WAVs will be kept.");
        SetStatus($"Recording {selected.Length} selected songs. Keep the app’s Chrome window open; avoid other playback in that window. You can use your usual browser separately.");
        await new RecorderQueue(browser).RunAsync(selected, folder.Text, (track, message, fraction) => {
            var terminal = message.StartsWith("Saved") || message.StartsWith("Failed") || message.StartsWith("Already") || message.StartsWith("Stopped");
            track.Status = message; tracks.ResetItem(tracks.IndexOf(track));
            progress.Value = (int)(fraction * 100); status.Text = $"{track.Title} — {message}";
            if (terminal) log.AppendText($"{DateTime.Now:HH:mm:ss}  {track.Title}: {message}{Environment.NewLine}");
        }, token, recordAgain);
        var saved = selected.Count(t => t.Status == "Saved WAV");
        var skipped = selected.Count(t => t.Status.StartsWith("Already saved:"));
        SetStatus($"Queue finished: {saved} newly recorded, {skipped} skipped (existing WAV), {selected.Length - saved - skipped} need attention. See each track’s status.");
    }
    internal void CheckUi(string directory)
    {
        catalog.Add(new() { Id = "ExampleTrack0001", Title = "Night Drive", Selected = true });
        catalog.Add(new() { Id = "ExampleTrack0002", Title = "Sunrise" });
        search.Text = "night";
        if (tracks.Count != 1 || tracks[0].Title != "Night Drive") throw new Exception("Visible track-name filter failed.");
        search.Text = "sun";
        if (tracks.Count != 1 || tracks.Any(t => t.Selected)) throw new Exception("Hidden selected tracks leaked into the visible queue.");
        search.Text = "";
        if (tracks.Count != 2 || !tracks[0].Selected) throw new Exception("Clearing name filter lost tracks or selection.");
        Size = MinimumSize; Show(); Application.DoEvents();
        Directory.CreateDirectory(directory);
        using var bitmap = new Bitmap(Width, Height); DrawToBitmap(bitmap, new Rectangle(Point.Empty, Size));
        bitmap.Save(Path.Combine(directory, "app.png"));
        if (pauseScan.Bottom > pauseScan.Parent!.ClientSize.Height || clearScan.Right > clearScan.Parent!.ClientSize.Width)
            throw new Exception($"Scan controls clipped: pause bottom {pauseScan.Bottom}, bar height {pauseScan.Parent.ClientSize.Height}, clear right {clearScan.Right}, bar width {clearScan.Parent!.ClientSize.Width}.");
        foreach (var bar in Controls.OfType<TableLayoutPanel>().SelectMany(p => p.Controls.OfType<FlowLayoutPanel>()))
            foreach (Control child in bar.Controls)
                if (child.Right > bar.ClientSize.Width || child.Bottom > bar.ClientSize.Height)
                    throw new Exception($"Control clipped at minimum size: {child.Text}");
        File.WriteAllText(Path.Combine(directory, "ui-check.json"), "{\"Passed\":true,\"Checks\":\"Name filter, hidden selection, reset and minimum-size controls\"}");
        closing = true; Close();
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

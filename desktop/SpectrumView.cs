namespace AssetRaider.Desktop;

internal sealed class SpectrumView : Control
{
    private readonly System.Windows.Forms.Timer animation = new() { Interval = 40 };
    public SpectrumView() { DoubleBuffered = true; BackColor = Color.FromArgb(24, 31, 25); AccessibleName = "Live audio spectrum"; animation.Tick += (_, _) => Invalidate(); animation.Start(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); var levels = AudioSpectrum.Shared.Read();
        using var grid = new Pen(Color.FromArgb(46, 60, 43)); using var bar = new SolidBrush(Color.FromArgb(160, 222, 83));
        using var cap = new SolidBrush(Color.FromArgb(218, 248, 162)); using var text = new SolidBrush(Color.FromArgb(188, 212, 159));
        for (var y = 22; y < Height - 15; y += 14) e.Graphics.DrawLine(grid, 10, y, Width - 10, y);
        var width = (Width - 20f) / AudioSpectrum.Bands; var height = Math.Max(1, Height - 36);
        for (var i = 0; i < levels.Length; i++) {
            var h = (float)(levels[i] * height); var x = 10 + i * width; var y = Height - 16 - h;
            e.Graphics.FillRectangle(bar, x, y, Math.Max(1, width - 4), Math.Max(1, h));
            if (h > 2) e.Graphics.FillRectangle(cap, x, y, Math.Max(1, width - 4), 2);
        }
        using var font = new Font("Segoe UI", 8);
        e.Graphics.DrawString("AUDIO SPECTRUM", font, text, 10, 2);
        e.Graphics.DrawString("50 Hz                         250 Hz                         1 kHz                         4 kHz                         18 kHz", font, text, 10, Height - 15);
    }
    protected override void Dispose(bool disposing) { if (disposing) animation.Dispose(); base.Dispose(disposing); }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace AssetRaider.Desktop;

internal sealed class SpectrumView : Control
{
    private readonly DispatcherTimer animation = new() { Interval = TimeSpan.FromMilliseconds(40) };
    public SpectrumView() { Height = 62; animation.Tick += (_, _) => InvalidateVisual(); AttachedToVisualTree += (_, _) => animation.Start(); DetachedFromVisualTree += (_, _) => animation.Stop(); }
    public override void Render(DrawingContext context)
    {
        base.Render(context); context.FillRectangle(Brush.Parse("#192219"), new Rect(Bounds.Size));
        var grid = new Pen(Brush.Parse("#34422E"), 1); var bar = Brush.Parse("#A0DE53"); var cap = Brush.Parse("#DAF8A2");
        for (var y = 10; y < Bounds.Height; y += 14) context.DrawLine(grid, new Point(8, y), new Point(Bounds.Width - 8, y));
        var levels = AudioSpectrum.Shared.Read(); var width = Math.Max(1, (Bounds.Width - 16) / AudioSpectrum.Bands);
        for (var i = 0; i < levels.Length; i++) {
            var h = Math.Max(1, levels[i] * (Bounds.Height - 8)); var x = 8 + i * width; var y = Bounds.Height - 4 - h;
            context.FillRectangle(bar, new Rect(x, y, Math.Max(1, width - 4), h));
            if (h > 2) context.FillRectangle(cap, new Rect(x, y, Math.Max(1, width - 4), 2));
        }
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
namespace AssetRaider.Desktop;

internal sealed class ArtworkThumbnail : Border
{
    private readonly string url;
    private Bitmap? bitmap;
    private int generation;
    public ArtworkThumbnail(string artworkUrl)
    {
        url = artworkUrl; Width = Height = 42; CornerRadius = new CornerRadius(4); Background = Brush.Parse("#263226");
        Placeholder();
        AttachedToVisualTree += async (_, _) => {
            var current = ++generation;
            try {
                var bytes = await ArtworkDownload.ReadAsync(url);
                if (bytes == null || generation != current) return;
                using var stream = new MemoryStream(bytes);
                bitmap = Bitmap.DecodeToWidth(stream, 48);
                Child = new Image { Source = bitmap, Stretch = Stretch.UniformToFill };
            } catch { Placeholder(); }
        };
        DetachedFromVisualTree += (_, _) => { generation++; Child = null; bitmap?.Dispose(); bitmap = null; Placeholder(); };
    }
    private void Placeholder() => Child = new TextBlock { Text = "♪", FontSize = 22, Foreground = Brush.Parse("#8CA578"), HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
}

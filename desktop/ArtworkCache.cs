using SkiaSharp;
namespace AssetRaider.Desktop;

internal sealed class ArtworkCache(Action changed) : IDisposable
{
    private readonly Dictionary<string, Image?> images = [];
    private int pending;
    private bool disposed;
    public Image? Get(string url)
    {
        if (disposed || url.Length == 0) return null;
        if (images.TryGetValue(url, out var image)) return image;
        if (pending >= 8) return null;
        if (images.Count >= 96) { var first = images.First(); images.Remove(first.Key); first.Value?.Dispose(); }
        images[url] = null; pending++; _ = LoadAsync(url); return null;
    }
    private async Task LoadAsync(string url)
    {
        Image? thumbnail = null;
        try {
            var bytes = await ArtworkDownload.ReadAsync(url);
            if (bytes != null) {
                using var data = SKData.CreateCopy(bytes); using var codec = SKCodec.Create(data);
                if (codec != null && (long)codec.Info.Width * codec.Info.Height <= 8_000_000) {
                    using var decoded = SKBitmap.Decode(codec);
                    using var small = decoded?.Resize(new SKImageInfo(48,48), SKFilterQuality.Medium);
                    if (small != null) {
                        using var png = small.Encode(SKEncodedImageFormat.Png,100); using var stream = new MemoryStream(png.ToArray());
                        using var source = Image.FromStream(stream); thumbnail = new Bitmap(source);
                    }
                }
            }
        } catch { }
        finally { pending--; }
        if (disposed || !images.ContainsKey(url)) { thumbnail?.Dispose(); return; }
        images[url] = thumbnail; changed();
    }
    public void Dispose() { disposed = true; foreach (var image in images.Values) image?.Dispose(); images.Clear(); }
}

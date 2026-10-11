using System.Net;
namespace AssetRaider.Desktop;

// Only visible rows request artwork. Bounded downloads and concurrency keep scanning responsive.
public static class ArtworkDownload
{
    private static readonly HttpClient Client = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(12) };
    private static readonly SemaphoreSlim Slots = new(4);
    static ArtworkDownload() { Client.DefaultRequestHeaders.UserAgent.ParseAdd("AssetRaider/1.0"); Client.DefaultRequestHeaders.Accept.ParseAdd("image/*"); }
    public static async Task<byte[]?> ReadAsync(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !uri.IsDefaultPort || uri.IsLoopback || IPAddress.TryParse(uri.Host, out _) || !uri.Host.Contains('.')) return null;
        await Slots.WaitAsync();
        try {
            using var response = await Client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
            const int limit = 2 * 1024 * 1024;
            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > limit || !(response.Content.Headers.ContentType?.MediaType?.StartsWith("image/") ?? false)) return null;
            await using var source = await response.Content.ReadAsStreamAsync(); using var bytes = new MemoryStream();
            var buffer = new byte[8192];
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(12));
            while (true) { var count = await source.ReadAsync(buffer, timeout.Token); if (count == 0) break; if (bytes.Length + count > limit) return null; bytes.Write(buffer, 0, count); }
            return bytes.ToArray();
        } catch { return null; }
        finally { Slots.Release(); }
    }
}

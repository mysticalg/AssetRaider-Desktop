namespace AssetRaider.Desktop;

public sealed record MusicSite(string Id, string Name, string LibraryUrl, string LinkSelector, string PlaySelector, string ProfileFolder)
{
    public static readonly MusicSite Udio = new("udio", "Udio", "https://www.udio.com/library", "main a[href*='/songs/']", "main button[aria-label='Play']", "ChromeProfile");
    public static readonly MusicSite Suno = new("suno", "Suno", "https://suno.com/create", "[data-testid='clip-row'] a[href*='/song/'], [role='group'] a[href*='/song/']", "button[aria-label='Play']", "ChromeProfile-Suno");
    public static MusicSite FromId(string id) => id switch { "udio" => Udio, "suno" => Suno, _ => throw new ArgumentException("Unknown music site.") };
    public bool OwnsUrl(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "https" &&
        (uri.Host == new Uri(LibraryUrl).Host || uri.Host == (Id == "udio" ? "udio.com" : "www.suno.com"));
    public bool IsLibraryUrl(string url) => OwnsUrl(url) && new Uri(url).AbsolutePath.StartsWith(Id == "suno" ? "/create" : "/library", StringComparison.Ordinal);
}

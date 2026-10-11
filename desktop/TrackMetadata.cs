using System.Text.Json;
using Microsoft.Playwright;
namespace AssetRaider.Desktop;

public sealed record TrackMetadata(string CreatedAt, string ReleasedAt, string Version, string Prompt)
{
    private static string Text(JsonElement element, string name) => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
    public static Dictionary<string, TrackMetadata> Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var result = new Dictionary<string, TrackMetadata>(); var remaining = 100000;
        void Visit(JsonElement element)
        {
            if (--remaining < 0) return;
            if (element.ValueKind == JsonValueKind.Array) { foreach (var item in element.EnumerateArray()) Visit(item); return; }
            if (element.ValueKind != JsonValueKind.Object) return;
            var id = Text(element,"id");
            if (Guid.TryParse(id,out _) && element.TryGetProperty("metadata",out var meta) && meta.ValueKind == JsonValueKind.Object) {
                var version = "";
                if (meta.TryGetProperty("model_badges",out var badges) && badges.ValueKind == JsonValueKind.Object && badges.TryGetProperty("songrow",out var badge)) version = Text(badge,"display_name");
                if (version.Length == 0) version = Text(element,"major_model_version");
                if (version.Length == 0) version = Text(element,"model_name");
                var styles = Text(meta,"tags"); var lyrics = Text(meta,"prompt"); var excluded = Text(meta,"negative_tags");
                if (styles.Length == 0) styles = Text(meta,"gpt_description_prompt");
                var prompt = string.Join("\n\n", new[] { styles.Length > 0 ? "Style prompt:\n" + styles : "", lyrics.Length > 0 ? "Lyrics / generation prompt:\n" + lyrics : "", excluded.Length > 0 ? "Excluded styles:\n" + excluded : "" }.Where(s=>s.Length>0));
                var release = Text(element,"published_at"); if (release.Length == 0) release = Text(element,"released_at");
                result[id] = new(Text(element,"created_at"), release, version, prompt);
            }
            foreach (var value in element.EnumerateObject()) Visit(value.Value);
        }
        Visit(document.RootElement); return result;
    }
    internal static void Check()
    {
        const string id="11111111-1111-4111-8111-111111111111";
        var data=Parse("{\"clips\":[{\"id\":\""+id+"\",\"created_at\":\"2026-01-02T03:04:05Z\",\"is_public\":true,\"major_model_version\":\"v5\",\"metadata\":{\"tags\":\"full style\\nsecond line\",\"prompt\":\"verse\\nchorus\",\"model_badges\":{\"songrow\":{\"display_name\":\"V5.5\"}}}}]}")[id];
        if(data.ReleasedAt.Length!=0 || data.Version!="V5.5" || !data.Prompt.Contains("verse\nchorus") || !data.Prompt.Contains("full style\nsecond line")) throw new Exception("Full prompt, badge or unavailable release date failed.");
        var published=Parse("{\"id\":\""+id+"\",\"published_at\":\"2026-02-03T00:00:00Z\",\"metadata\":{}}")[id];
        if(published.ReleasedAt!="2026-02-03T00:00:00Z") throw new Exception("Explicit publication timestamp lost.");
    }
}

// Observe only clip metadata already requested by the signed-in library; never request credentials or audio URLs.
public sealed class LibraryMetadataMonitor : IDisposable
{
    private readonly IPage page;
    private readonly object gate = new();
    private readonly Dictionary<string, TrackMetadata> clips = [];
    private readonly List<Task> pending = [];
    private bool disposed;
    public LibraryMetadataMonitor(IPage page) { this.page=page; page.Response += Response; }
    private void Response(object? sender, IResponse response)
    {
        if (!Uri.TryCreate(response.Url,UriKind.Absolute,out var uri) || !uri.Host.EndsWith(".suno.com",StringComparison.OrdinalIgnoreCase) || !(uri.AbsolutePath.StartsWith("/api/feed/") || uri.AbsolutePath=="/api/unified/feed")) return;
        lock(gate) { if(disposed) return; pending.RemoveAll(t=>t.IsCompleted); pending.Add(Read(response)); }
    }
    private async Task Read(IResponse response)
    {
        try {
            var body=await response.BodyAsync(); if(body.Length>8*1024*1024) return;
            var found=TrackMetadata.Parse(System.Text.Encoding.UTF8.GetString(body));
            lock(gate) { if(disposed) return; foreach(var pair in found) { if(clips.Count>=20000 && !clips.ContainsKey(pair.Key)) clips.Remove(clips.First().Key); clips[pair.Key]=pair.Value; } }
        } catch { }
    }
    public async Task EnrichAsync(Track[] tracks)
    {
        Task[] work; lock(gate) work=pending.ToArray();
        try { await Task.WhenAll(work).WaitAsync(TimeSpan.FromSeconds(2)); } catch { }
        lock(gate) foreach(var track in tracks) if(clips.TryGetValue(track.Id,out var data)) { track.CreatedAt=data.CreatedAt; track.ReleasedAt=data.ReleasedAt; track.SunoVersion=data.Version; if(data.Prompt.Length>0) track.Prompt=data.Prompt; }
    }
    public void Dispose() { page.Response -= Response; lock(gate) { disposed=true; clips.Clear(); pending.Clear(); } }
}

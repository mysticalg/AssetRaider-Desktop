using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Playwright;

namespace AssetRaider.Desktop;

// These describe the browser's encoded input, never the capture device or WAV output.
public sealed record SourceQuality(string? Codec = null, int? SampleRateHz = null, long? BitrateBps = null, int? Channels = null)
{
    public static SourceQuality Unknown { get; } = new();
    public string Measurement => "Chrome source metadata; bitrate is an approximate stream average, not an instantaneous encoder setting.";
    [JsonIgnore] public string Display {
        get {
            var parts = new List<string>();
            if (Codec != null) parts.Add(Codec);
            if (BitrateBps is > 0) parts.Add($"~{(BitrateBps.Value / 1000d).ToString("0.#", CultureInfo.InvariantCulture)} kbps");
            if (SampleRateHz is > 0) parts.Add($"{(SampleRateHz.Value / 1000d).ToString("0.###", CultureInfo.InvariantCulture)} kHz");
            if (Channels is > 0) parts.Add(Channels switch { 1 => "mono", 2 => "stereo", _ => $"{Channels} channels" });
            return parts.Count > 0 ? string.Join(" · ", parts) : "Source quality unavailable";
        }
    }
}

// Bounded, per-recording state. URLs stay in memory and are never written into the ledger/log.
internal sealed class SourceQualityCollector
{
    private sealed class Player
    {
        public string Url = "";
        public Dictionary<string, string> Properties = [];
    }
    private readonly Dictionary<string, Player> players = [];
    private readonly object gate = new();
    public void Accept(string method, JsonElement message)
    {
        lock (gate)
        {
            try
            {
                var id = message.GetProperty("playerId").GetString();
                if (string.IsNullOrEmpty(id)) return;
                if (!players.TryGetValue(id, out var player))
                {
                    if (players.Count >= 256) return;
                    players[id] = player = new();
                }
                if (method == "Media.playerPropertiesChanged")
                {
                    foreach (var property in message.GetProperty("properties").EnumerateArray())
                    {
                        var name = property.GetProperty("name").GetString();
                        if (name is not ("kAudioTracks" or "kVideoTracks" or "kBitrate")) continue;
                        var value = property.GetProperty("value");
                        if (value.ValueKind == JsonValueKind.Null) player.Properties.Remove(name);
                        else if (value.ValueKind == JsonValueKind.String) player.Properties[name] = value.GetString()!;
                    }
                }
                else if (method == "Media.playerEventsAdded")
                {
                    foreach (var item in message.GetProperty("events").EnumerateArray())
                    {
                        using var doc = JsonDocument.Parse(item.GetProperty("value").GetString()!);
                        var value = doc.RootElement;
                        if (!value.TryGetProperty("event", out var kind)) continue;
                        if (kind.GetString() == "kWebMediaPlayerDestroyed") { players.Remove(id); break; }
                        if (kind.GetString() == "kLoad" && value.TryGetProperty("url", out var url))
                        {
                            // A reused player must not retain the last stream's encoding.
                            if (player.Url.Length > 0) player.Properties.Clear();
                            player.Url = url.GetString() ?? "";
                        }
                    }
                }
            }
            // Diagnostics are optional: malformed/new Chrome metadata cannot fail recording.
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException or ArgumentException) { }
        }
    }

    public SourceQuality Read(string source)
    {
        if (string.IsNullOrEmpty(source)) return SourceQuality.Unknown;
        lock (gate)
        {
            var matches = players.Values.Where(p => p.Url == source).Take(2).ToArray();
            // Never select a cover, keep-alive clip, or an ambiguous player by proximity/duration.
            if (matches.Length != 1) return SourceQuality.Unknown;
            var properties = matches[0].Properties;
            try
            {
                if (!properties.TryGetValue("kAudioTracks", out var audioJson)) return SourceQuality.Unknown;
                using var audio = JsonDocument.Parse(audioJson);
                if (audio.RootElement.ValueKind != JsonValueKind.Array || audio.RootElement.GetArrayLength() != 1) return SourceQuality.Unknown;
                var config = audio.RootElement[0];
                var codec = config.TryGetProperty("codec", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null;
                codec = codec?.ToLowerInvariant() switch
                {
                    "mp3" => "MP3", "aac" => "AAC", "opus" => "Opus", "vorbis" => "Vorbis", "flac" => "FLAC",
                    "pcm" or "pcm_s16le" or "pcm_s24le" or "pcm_f32le" => "PCM", _ => null
                };
                var sampleRate = PositiveInteger(config, "samples per second", 768000);
                var channels = PositiveInteger(config, "channels", 64);
                long? bitrate = null;
                // kBitrate is for the entire demuxed resource. Reject it for video/multi-audio resources.
                var hasVideo = false;
                if (properties.TryGetValue("kVideoTracks", out var videoJson))
                {
                    using var video = JsonDocument.Parse(videoJson);
                    hasVideo = video.RootElement.ValueKind != JsonValueKind.Array || video.RootElement.GetArrayLength() != 0;
                }
                if (!hasVideo && properties.TryGetValue("kBitrate", out var bits))
                {
                    using var parsed = JsonDocument.Parse(bits);
                    if (parsed.RootElement.ValueKind == JsonValueKind.Number && parsed.RootElement.TryGetInt64(out var b) && b > 0 && b <= 1_000_000_000) bitrate = b;
                }
                return new(codec, sampleRate, bitrate, channels);
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException) { return SourceQuality.Unknown; }
        }
    }

    private static int? PositiveInteger(JsonElement value, string key, int maximum) =>
        value.TryGetProperty(key, out var property) && property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var number) && number > 0 && number <= maximum ? number : null;
}

internal sealed class SourceQualityMonitor : IAsyncDisposable
{
    private readonly SourceQualityCollector collector = new();
    private ICDPSession? session;
    private void Properties(object? sender, JsonElement? e) { if (e is { } value) collector.Accept("Media.playerPropertiesChanged", value); }
    private void Events(object? sender, JsonElement? e) { if (e is { } value) collector.Accept("Media.playerEventsAdded", value); }
    public SourceQuality Read(string source) => collector.Read(source);

    public static async Task<SourceQualityMonitor> AttachAsync(IPage page)
    {
        var monitor = new SourceQualityMonitor();
        try
        {
            monitor.session = await page.Context.NewCDPSessionAsync(page).WaitAsync(TimeSpan.FromSeconds(3));
            monitor.session.Event("Media.playerPropertiesChanged").OnEvent += monitor.Properties;
            monitor.session.Event("Media.playerEventsAdded").OnEvent += monitor.Events;
            await monitor.session.SendAsync("Media.enable").WaitAsync(TimeSpan.FromSeconds(3));
        }
        catch { await monitor.DisposeAsync(); }
        return monitor;
    }

    public async ValueTask DisposeAsync()
    {
        if (session is not { } old) return;
        session = null;
        old.Event("Media.playerPropertiesChanged").OnEvent -= Properties;
        old.Event("Media.playerEventsAdded").OnEvent -= Events;
        try { await old.DetachAsync().WaitAsync(TimeSpan.FromSeconds(2)); } catch { }
    }
}

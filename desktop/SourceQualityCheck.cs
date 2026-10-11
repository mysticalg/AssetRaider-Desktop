using System.Text.Json;
using Microsoft.Playwright;

namespace AssetRaider.Desktop;

internal static class SourceQualityCheck
{
    public static void Run()
    {
        var collector = new SourceQualityCollector();
        void Load(string id, string url) => collector.Accept("Media.playerEventsAdded", JsonSerializer.SerializeToElement(new { playerId = id, events = new[] { new { value = JsonSerializer.Serialize(new { @event = "kLoad", url }) } } }));
        void Props(string id, params (string Name, string? Value)[] values) => collector.Accept("Media.playerPropertiesChanged", JsonSerializer.SerializeToElement(new { playerId = id, properties = values.Select(p => new { name = p.Name, value = p.Value }) }));
        const string audio = "[{\"codec\":\"mp3\",\"samples per second\":44100,\"channels\":2}]";
        Load("keepalive", "https://example.test/sil-100.mp3"); Props("keepalive", ("kAudioTracks", audio), ("kBitrate", "32000"));
        Load("cover", "blob:cover"); Props("cover", ("kAudioTracks", audio), ("kBitrate", "999000"), ("kVideoTracks", "[{\"codec\":\"h264\"}]"));
        // Chrome may replay properties before events on attachment.
        Props("song", ("kAudioTracks", audio), ("kBitrate", "96000")); Load("song", "blob:song");
        var expected = new SourceQuality("MP3", 44100, 96000, 2);
        Require(collector.Read("blob:song") == expected, "Selected audio source did not match its own metadata.");
        Require(collector.Read("blob:cover").BitrateBps == null, "Combined video bitrate was labelled as audio bitrate.");
        Require(collector.Read("blob:missing") == SourceQuality.Unknown, "Unmatched source inherited another player's quality.");
        Props("song", ("kBitrate", "128000")); Require(collector.Read("blob:song").BitrateBps == 128000, "Changing bitrate did not refresh.");
        Props("song", ("kBitrate", null)); Require(collector.Read("blob:song").BitrateBps == null, "Removed metadata was retained.");
        Load("other", "blob:song"); Require(collector.Read("blob:song") == SourceQuality.Unknown, "Ambiguous players were guessed.");
        Load("other", "blob:other");
        Load("song", "blob:new-song"); Require(collector.Read("blob:new-song") == SourceQuality.Unknown, "Reused player retained previous encoding.");
        Props("song", ("kAudioTracks", "[{\"codec\":\"aac\",\"samples per second\":48000,\"channels\":1}]"), ("kBitrate", "0"));
        Require(collector.Read("blob:new-song") == new SourceQuality("AAC", 48000, null, 1), "Zero bitrate was presented as a measurement.");
        Props("song", ("kAudioTracks", "[{},{}]")); Require(collector.Read("blob:new-song") == SourceQuality.Unknown, "Multi-track metadata was guessed.");
        Props("song", ("kAudioTracks", "bad json")); Require(collector.Read("blob:new-song") == SourceQuality.Unknown, "Malformed metadata failed open.");
        collector.Accept("Media.playerEventsAdded", JsonSerializer.SerializeToElement(new { playerId = "bad", events = new[] { new { value = "bad json" } } }));
        Require(expected.Display.Contains("~96 kbps") && expected.Display.Contains("44.1 kHz"), "Source quality units/approximation label incorrect.");
        Require(BrowserData.Decode<SourceQuality>(JsonSerializer.Serialize(expected)) == expected, "Quality did not round-trip through saved JSON.");
    }

    // Uses an isolated, headless Chrome instance and locally generated fixtures; never a user's profile.
    public static async Task<int> RunBrowserAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        try
        {
            Run();
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new() { Channel = "chrome", Headless = true, Args = ["--mute-audio"] });
            var page = await browser.NewPageAsync();
            var bytes = await File.ReadAllBytesAsync(Path.Combine(directory, "source-96k-44100.mp3"));
            await page.RouteAsync("https://quality.assetraider.test/**", async route => {
                if (route.Request.Url.EndsWith(".mp3")) await route.FulfillAsync(new() { ContentType = "audio/mpeg", BodyBytes = bytes });
                else await route.FulfillAsync(new() { ContentType = "text/html", Body = "<main><button aria-label='Play' onclick='song.play()'>Play</button></main><script>window.song=new Audio(); fetch('/source.mp3').then(r=>r.blob()).then(b=>song.src=URL.createObjectURL(b));</script>" });
            });
            await page.GotoAsync("https://quality.assetraider.test/song/ExampleTrack0001");
            await using var monitor = await SourceQualityMonitor.AttachAsync(page);
            await page.WaitForFunctionAsync("() => song.readyState >= 2");
            var duration = await page.EvaluateAsync<double>(MusicBrowser.Script("player"), new { site = "suno", id = "ExampleTrack0001" });
            var state = await BrowserData.ReadAsync<PlayerState>(page, "() => window.__assetRaiderDesktop.state()");
            SourceQuality quality = SourceQuality.Unknown;
            for (var i = 0; i < 40; i++)
            {
                quality = monitor.Read(state.Source);
                if (quality.BitrateBps != null && quality.SampleRateHz != null) break;
                await Task.Delay(100);
            }
            await page.EvaluateAsync("() => window.__assetRaiderDesktop.stop()");
            Require(state.Source.StartsWith("blob:"), "Integration check must use the detached blob player.");
            Require(quality.Codec == "MP3" && quality.SampleRateHz == 44100 && quality.Channels == 2 && quality.BitrateBps is >= 95000 and <= 100000, "Chrome metadata did not match the known 96 kbps / 44.1 kHz MP3: " + quality.Display);
            File.WriteAllText(Path.Combine(directory, "quality-check.json"), JsonSerializer.Serialize(new { Passed = true, Scope = "Isolated Chrome, generated MP3 fixture, real recording player preparation", duration, quality }, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }
        catch (Exception ex) { File.WriteAllText(Path.Combine(directory, "quality-check.json"), ex.ToString()); return 1; }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}

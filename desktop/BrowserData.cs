using System.Text.Json;
using Microsoft.Playwright;

namespace AssetRaider.Desktop;

public static class BrowserData
{
    // Playwright's EvaluateAsync<T> supports its own transport types, not arbitrary CLR records.
    // Transport JSON as a string and deserialize it explicitly for every structured page result.
    public static async Task<T> ReadAsync<T>(IPage page, string script, object? argument = null)
    {
        var json = await page.EvaluateAsync<string>("async arg => JSON.stringify(await (" + script + ")(arg))", argument);
        return Decode<T>(json);
    }
    public static T Decode<T>(string json) => JsonSerializer.Deserialize<T>(json)
        ?? throw new IOException("The music page returned an empty result. Reload its library and try again.");
}

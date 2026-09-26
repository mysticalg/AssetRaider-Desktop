using System.Diagnostics;
namespace AssetRaider.Desktop;

// Windows captures the dedicated process tree directly; no virtual device is needed.
internal static class PlatformAudio
{
    public static void ConfigureBrowser(ProcessStartInfo start) { }
    public static Task PrepareBrowserAsync() => Task.CompletedTask;
    public static Task ReleaseBrowserAsync() => Task.CompletedTask;
}

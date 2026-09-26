using System.Diagnostics;

namespace AssetRaider.Desktop;

internal static class PlatformAudio
{
    // A fresh name per app instance avoids capturing any existing desktop/microphone source.
    public static string Sink { get; } = "assetraider_" + Environment.ProcessId + "_" + Guid.NewGuid().ToString("N")[..8];
    private static string? module;
    public static void ConfigureBrowser(ProcessStartInfo start)
    {
        if (OperatingSystem.IsLinux()) start.Environment["PULSE_SINK"] = Sink;
    }
    public static async Task PrepareBrowserAsync()
    {
        if (!OperatingSystem.IsLinux() || module != null) return;
        _ = FindTool("parec");
        module = (await Command("pactl", "load-module", "module-null-sink", "sink_name=" + Sink,
            "format=s16le", "rate=48000", "channels=2", "sink_properties=device.description=AssetRaider")).Trim();
        if (!uint.TryParse(module, out _)) { module = null; throw new IOException("The sound server could not create a dedicated recording channel."); }
    }
    public static async Task ReleaseBrowserAsync()
    {
        var owned = module; module = null;
        if (owned != null) try { await Command("pactl", "unload-module", owned); } catch { }
    }
    public static string FindTool(string name)
    {
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "/usr/bin:/bin").Split(Path.PathSeparator))
        {
            var candidate = Path.Combine(dir, name);
            if (File.Exists(candidate)) return candidate;
        }
        throw new IOException($"Missing {name}. Install pulseaudio-utils (Debian/Ubuntu) or pulseaudio-utils (Fedora) and use a running PulseAudio or PipeWire-Pulse desktop session.");
    }
    public static async Task<string> Command(string tool, params string[] args)
    {
        var start = new ProcessStartInfo(FindTool(tool)) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new IOException("Could not start " + tool);
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); }
        catch { try { process.Kill(); } catch { } throw new IOException(tool + " timed out. Check the desktop audio server."); }
        if (process.ExitCode != 0) throw new IOException(tool + ": " + (await error).Trim());
        return await output;
    }
}

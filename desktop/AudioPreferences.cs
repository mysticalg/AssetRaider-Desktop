using System.Text.Json;
namespace AssetRaider.Desktop;

public sealed class AudioPreferences
{
    public double[] Gains { get; set; } = new double[8];
    public double GainDb { get; set; }
    public bool Enabled { get; set; } = true;
    public int FrameRate { get; set; } = 60;
    public string Style { get; set; } = "Spectrum";
    public static readonly int[] Rates = [15, 24, 30, 45, 60, 90, 120];
    public static readonly string[] Styles = ["Spectrum", "Neon wave", "Mirror", "Orbit", "Fire"];
    public static readonly int[] Frequencies = [63, 125, 250, 500, 1000, 2000, 4000, 8000];
    private static string FilePath => Path.Combine(MusicBrowser.AppData, "audio-settings.json");
    public static AudioPreferences Load(string? path = null)
    {
        AudioPreferences s;
        try { s = JsonSerializer.Deserialize<AudioPreferences>(File.ReadAllText(path ?? FilePath)) ?? new(); } catch { s = new(); }
        if (s.Gains == null || s.Gains.Length != 8) s.Gains = new double[8];
        s.Gains = s.Gains.Select(v => double.IsFinite(v) ? Math.Clamp(v, -12, 12) : 0).ToArray();
        s.GainDb = double.IsFinite(s.GainDb) ? Math.Clamp(s.GainDb,-12,12) : 0;
        if (!Rates.Contains(s.FrameRate)) s.FrameRate = 60;
        if (!Styles.Contains(s.Style)) s.Style = Styles[0];
        return s;
    }
    public void Save(string? path = null)
    {
        path ??= FilePath; Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(this)); File.Move(path + ".tmp", path, true);
    }
}

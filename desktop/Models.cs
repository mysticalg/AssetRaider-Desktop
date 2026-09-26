using System.Text.Json;
using System.Text.RegularExpressions;
using NAudio.Wave;

namespace AssetRaider.Desktop;

public sealed class Track
{
    public bool Selected { get; set; }
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Duration { get; set; } = "";
    public string Status { get; set; } = "Ready";
    public string Site { get; set; } = "udio";
    public string Key => Site + ":" + Id;
    public string Url => MusicSite.FromId(Site).Id == "suno" ? "https://suno.com/song/" + Id : "https://www.udio.com/songs/" + Id;
}

public sealed record Completed(string Id, string Title, string FileName, long Bytes, double Seconds);

public sealed class RecordingLibrary(string directory)
{
    public string DirectoryPath { get; } = Path.GetFullPath(directory);
    private string LedgerPath => Path.Combine(DirectoryPath, "assetraider-completed.json");
    private Dictionary<string, Completed> entries = [];
    public void Load()
    {
        Directory.CreateDirectory(DirectoryPath);
        if (!File.Exists(LedgerPath)) return;
        // A damaged ledger must not cause completed output to be overwritten.
        entries = JsonSerializer.Deserialize<Dictionary<string, Completed>>(File.ReadAllText(LedgerPath))
            ?? throw new IOException("The completed-track list is invalid. Choose another output folder or restore that JSON file.");
        // Previous desktop releases stored Udio IDs without a site prefix.
        foreach (var pair in entries.Where(p => !p.Key.Contains(':')).ToArray())
        {
            entries.TryAdd("udio:" + pair.Key, pair.Value);
            entries.Remove(pair.Key);
        }
    }
    public bool IsComplete(Track track)
    {
        if (!entries.TryGetValue(track.Key, out var entry) || Path.GetFileName(entry.FileName) != entry.FileName) return false;
        var path = Path.Combine(DirectoryPath, entry.FileName);
        try
        {
            if (new FileInfo(path).Length != entry.Bytes) return false;
            using var reader = new WaveFileReader(path);
            return reader.TotalTime.TotalSeconds >= entry.Seconds - .1 && reader.Length > 0;
        }
        catch { return false; }
    }
    public static string SafeName(Track track)
    {
        _ = MusicSite.FromId(track.Site);
        if (!Regex.IsMatch(track.Id, @"\A[a-zA-Z0-9-]{8,64}\z")) throw new ArgumentException("Invalid song ID.");
        var title = string.Concat(track.Title.Select(c => Path.GetInvalidFileNameChars().Contains(c) || char.IsControl(c) ? '_' : c)).Trim().TrimEnd('.');
        if (title.Length > 70) title = title[..70];
        return $"{(track.Site == "suno" ? "Suno_" : "")}{(title.Length == 0 ? "Track" : title)}__{track.Id}";
    }
    public (string Partial, string Final) Allocate(Track track)
    {
        var stem = SafeName(track);
        for (var n = 0; n < 10000; n++)
        {
            var path = Path.Combine(DirectoryPath, stem + (n == 0 ? "" : $" ({n})"));
            if (!File.Exists(path + ".wav") && !File.Exists(path + ".partial.wav")) return (path + ".partial.wav", path + ".wav");
        }
        throw new IOException("Too many files already share this track name.");
    }
    public void Complete(Track track, string partial, string final, double seconds)
    {
        using (var reader = new WaveFileReader(partial))
            if (reader.TotalTime.TotalSeconds < seconds - 1) throw new IOException("The WAV is shorter than the song; it was kept as a partial recording.");
        File.Move(partial, final, false);
        entries[track.Key] = new(track.Id, track.Title, Path.GetFileName(final), new FileInfo(final).Length, seconds);
        var temp = LedgerPath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, LedgerPath, true);
    }
}

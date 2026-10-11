namespace AssetRaider.Desktop;

public enum RepeatMode { Off, All, One }

public sealed class PlaybackPlaylist
{
    private Track[] tracks = [];
    private readonly HashSet<string> played = [];
    private bool shuffle;
    public RepeatMode Repeat { get; set; }
    public bool Shuffle { get => shuffle; set { if (shuffle == value) return; shuffle = value; played.Clear(); } }
    public void Update(IEnumerable<Track> visible) { tracks = visible.DistinctBy(t => t.Key).ToArray(); played.IntersectWith(tracks.Select(t => t.Key)); }
    public void Started(Track track) => played.Add(track.Key);
    public void Begin() => played.Clear();
    public bool CanAdvance(Track? current, bool automatic = false)
    {
        if (current == null) return false;
        if (automatic && Repeat == RepeatMode.One) return true;
        if (tracks.Length == 0) return false;
        if (Shuffle) return tracks.Any(t => t.Key != current.Key && !played.Contains(t.Key)) || Repeat != RepeatMode.Off;
        var index = Array.FindIndex(tracks, t => t.Key == current.Key);
        return index < tracks.Length - 1 || Repeat != RepeatMode.Off;
    }
    public Track? Next(Track current, bool automatic = false)
    {
        if (automatic && Repeat == RepeatMode.One) return current;
        if (!CanAdvance(current, automatic)) return null;
        if (Shuffle) {
            played.Add(current.Key);
            var options = tracks.Where(t => t.Key != current.Key && !played.Contains(t.Key)).ToArray();
            if (options.Length == 0) {
                played.Clear(); played.Add(current.Key);
                options = tracks.Where(t => t.Key != current.Key).ToArray();
            }
            return options.Length == 0 ? current : options[Random.Shared.Next(options.Length)];
        }
        var index = Array.FindIndex(tracks, t => t.Key == current.Key);
        return tracks[(index + 1) % tracks.Length];
    }
    internal static void Check()
    {
        var a = new Track {Id="A"}; var b = new Track {Id="B"}; var c = new Track {Id="C"};
        var queue = new PlaybackPlaylist(); queue.Update([a,b,c]); queue.Started(a);
        if (queue.Next(a) != b || queue.Next(c) != null) throw new Exception("Ordered playback/end failed.");
        queue.Repeat = RepeatMode.All; if (queue.Next(c) != a) throw new Exception("Repeat all did not wrap.");
        queue.Repeat = RepeatMode.One; if (queue.Next(b,true) != b || queue.Next(b) != c) throw new Exception("Repeat one/manual Next failed.");
        queue.Repeat = RepeatMode.Off; queue.Shuffle = true; queue.Started(a);
        var next = queue.Next(a)!; queue.Started(next); var last = queue.Next(next)!; queue.Started(last);
        if (new[] {a,next,last}.Distinct().Count() != 3 || queue.Next(last) != null) throw new Exception("Shuffle repeated tracks or failed to finish.");
        queue.Update([b]); queue.Repeat = RepeatMode.All;
        if (queue.Next(b) != b) throw new Exception("Single-track repeat failed.");
        queue.Update([]); queue.Repeat = RepeatMode.Off;
        if (queue.Next(b) != null) throw new Exception("Empty visible list played a hidden track.");
    }
}

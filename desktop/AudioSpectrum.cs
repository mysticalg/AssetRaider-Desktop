using System.Buffers.Binary;
using System.Numerics;

namespace AssetRaider.Desktop;

// Analyses only the recorder's dedicated browser PCM. No audio or files are retained.
public sealed class AudioSpectrum
{
    public static AudioSpectrum Shared { get; } = new();
    public const int Bands = 32;
    private const int Size = 2048;
    private readonly object gate = new();
    private readonly Complex[] left = new Complex[Size], right = new Complex[Size];
    private readonly double[] values = new double[Bands];
    private int position;
    private long updated;
    public void Reset() { lock (gate) { position = 0; Array.Clear(values); updated = 0; } }
    public double[] Read()
    {
        lock (gate) {
            var age = (Environment.TickCount64 - updated) / 1000d;
            var fade = age <= .1 ? 1 : Math.Exp(-(age - .1) * 6);
            return values.Select(v => v * fade).ToArray();
        }
    }
    public void Push(ReadOnlySpan<byte> pcm)
    {
        lock (gate) {
            for (var i = 0; i + 3 < pcm.Length; i += 4) {
                var window = .5 - .5 * Math.Cos(2 * Math.PI * position / (Size - 1));
                left[position] = new(BinaryPrimitives.ReadInt16LittleEndian(pcm.Slice(i, 2)) / 32768d * window, 0);
                right[position] = new(BinaryPrimitives.ReadInt16LittleEndian(pcm.Slice(i + 2, 2)) / 32768d * window, 0);
                if (++position != Size) continue;
                Transform(left); Transform(right); position = 0;
                for (var band = 0; band < Bands; band++) {
                    var low = Math.Max(1, (int)Math.Floor(50 * Math.Pow(360, band / (double)Bands) * Size / 48000));
                    var high = Math.Min(Size / 2, Math.Max(low + 1, (int)Math.Ceiling(50 * Math.Pow(360, (band + 1d) / Bands) * Size / 48000)));
                    double peak = 0;
                    for (var bin = low; bin < high; bin++) peak = Math.Max(peak, Math.Sqrt((left[bin].Magnitude * left[bin].Magnitude + right[bin].Magnitude * right[bin].Magnitude) / 2) * 4 / Size);
                    var level = Math.Clamp((20 * Math.Log10(Math.Max(peak, 1e-8)) + 65) / 65, 0, 1);
                    values[band] = Math.Max(level, values[band] * .78);
                }
                updated = Environment.TickCount64;
            }
        }
    }
    private static void Transform(Complex[] samples)
    {
        for (int i = 1, j = 0; i < Size; i++) {
            var bit = Size >> 1; for (; (j & bit) != 0; bit >>= 1) j ^= bit; j ^= bit;
            if (i < j) (samples[i], samples[j]) = (samples[j], samples[i]);
        }
        for (var length = 2; length <= Size; length <<= 1) {
            var step = Complex.FromPolarCoordinates(1, -2 * Math.PI / length);
            for (var start = 0; start < Size; start += length) {
                var factor = Complex.One;
                for (var offset = 0; offset < length / 2; offset++) {
                    var even = samples[start + offset]; var odd = samples[start + offset + length / 2] * factor;
                    samples[start + offset] = even + odd; samples[start + offset + length / 2] = even - odd; factor *= step;
                }
            }
        }
    }
    internal static void Check()
    {
        var spectrum = new AudioSpectrum(); var pcm = new byte[Size * 4];
        // Opposite-phase stereo must still produce a visible, correctly placed peak.
        for (var i = 0; i < Size; i++) {
            var value = (short)(10000 * Math.Sin(2 * Math.PI * 1000 * i / 48000));
            BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(i * 4, 2), value);
            BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(i * 4 + 2, 2), (short)-value);
        }
        spectrum.Push(pcm); var levels = spectrum.Read(); var peak = Array.IndexOf(levels, levels.Max());
        var expected = Math.Log(1000d / 50) / Math.Log(360) * Bands;
        if (Math.Abs(peak - expected) > 2 || levels.Max() < .5) throw new Exception("Spectrum frequency/stereo analysis failed.");
        spectrum.Reset(); if (spectrum.Read().Any(v => v != 0)) throw new Exception("Spectrum reset retained old audio.");
    }
}

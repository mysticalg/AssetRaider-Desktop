using System.Buffers.Binary;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace AssetRaider.Desktop;

public sealed class WavCapture : IAsyncDisposable
{
    private readonly WasapiRecorder recorder;
    private readonly WaveFileWriter writer;
    private readonly FileStream output;
    private Exception? fault;
    private long frames;
    private long audibleSamples;
    private long? nextPosition;
    private bool disposed;
    public double Seconds => Interlocked.Read(ref frames) / 48000d;
    public bool HasAudio => Interlocked.Read(ref audibleSamples) > 100;
    public Exception? Fault => fault;

    private WavCapture(WasapiRecorder recorder, string path)
    {
        this.recorder = recorder;
        output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        try { writer = new WaveFileWriter(output, recorder.WaveFormat); }
        catch { output.Dispose(); throw; }
        recorder.RecordingStopped += (_, e) => { if (e.Exception != null) fault = e.Exception; };
        recorder.DataAvailable += (buffer, flags, position, _) =>
        {
            if (fault != null) return;
            try
            {
                if (frames > 48000L * 1830) throw new IOException("Recording exceeded its time limit.");
                if (nextPosition.HasValue && position > nextPosition.Value)
                {
                    var gap = position - nextPosition.Value;
                    if (gap > 48000 * 2) throw new IOException("Audio capture lost more than two seconds; kept as a partial recording.");
                    WriteSilence((int)gap * 4);
                }
                if ((flags & AudioClientBufferFlags.Silent) != 0) WriteSilence(buffer.Length);
                else
                {
                    writer.Write(buffer);
                    long audible = 0;
                    for (var i = 0; i + 1 < buffer.Length; i += 2)
                        if (Math.Abs((int)BinaryPrimitives.ReadInt16LittleEndian(buffer.Slice(i, 2))) > 8) audible++;
                    Interlocked.Add(ref audibleSamples, audible);
                    Interlocked.Add(ref frames, buffer.Length / 4);
                }
                nextPosition = position + buffer.Length / 4;
            }
            catch (Exception ex) { fault = ex; }
        };
    }

    private void WriteSilence(int bytes)
    {
        Span<byte> zeros = stackalloc byte[4096];
        zeros.Clear();
        while (bytes > 0)
        {
            var count = Math.Min(bytes, zeros.Length);
            writer.Write(zeros[..count]);
            Interlocked.Add(ref frames, count / 4);
            bytes -= count;
        }
    }
    public static async Task<WavCapture> StartAsync(int processId, string path)
    {
        if (Environment.OSVersion.Version.Build < 20348) throw new PlatformNotSupportedException("Per-app recording needs Windows build 20348 or newer (Windows 11 recommended).");
        var recorder = await Task.Run(() => {
            if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 20348)) throw new PlatformNotSupportedException("Windows build 20348 or newer is required.");
            return new WasapiRecorderBuilder()
            .WithProcessLoopback((uint)processId, ProcessLoopbackMode.IncludeTargetProcessTree)
            .WithFormat(new WaveFormat(48000, 16, 2)).BuildAsync();
        });
        WavCapture? capture = null;
        try { capture = new WavCapture(recorder, path); recorder.StartRecording(); return capture; }
        catch { await recorder.DisposeAsync(); capture?.writer.Dispose(); capture?.output.Dispose(); throw; }
    }
    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        disposed = true;
        try { recorder.StopRecording(); await recorder.DisposeAsync(); }
        finally { try { writer.Dispose(); } finally { output.Dispose(); } }
    }
}

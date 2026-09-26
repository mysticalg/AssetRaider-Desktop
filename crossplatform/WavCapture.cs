using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;
using NAudio.Wave;

namespace AssetRaider.Desktop;

public sealed class WavCapture : IAsyncDisposable
{
    [DllImport("libc", SetLastError = true)] private static extern int kill(int pid, int signal);
    private readonly Process process;
    private readonly FileStream output;
    private readonly WaveFileWriter writer;
    private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Task pump;
    private readonly Task errors;
    private long frames, audible;
    private bool stopping, disposed;
    private string diagnostic = "";
    public double Seconds => Interlocked.Read(ref frames) / 48000d;
    public bool HasAudio => Interlocked.Read(ref audible) > 100;
    public Exception? Fault { get; private set; }

    private WavCapture(ProcessStartInfo start, string path)
    {
        output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        writer = new WaveFileWriter(output, new WaveFormat(48000, 16, 2));
        try { process = Process.Start(start) ?? throw new IOException("Could not start audio capture."); }
        catch { writer.Dispose(); output.Dispose(); throw; }
        errors = ReadErrors(); pump = Pump();
    }
    public static async Task<WavCapture> StartAsync(int processId, string path)
    {
        var start = new ProcessStartInfo { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true };
        if (OperatingSystem.IsMacOS())
        {
            if (!OperatingSystem.IsMacOSVersionAtLeast(14)) throw new PlatformNotSupportedException("macOS 14 Sonoma or newer is required.");
            start.FileName = Path.Combine(AppContext.BaseDirectory, "AssetRaider.Audio");
            start.ArgumentList.Add(processId.ToString());
        }
        else if (OperatingSystem.IsLinux())
        {
            start.FileName = PlatformAudio.FindTool("parec");
            foreach (var arg in new[] { "--raw", "--format=s16le", "--rate=48000", "--channels=2", "--latency-msec=50", "--device=" + PlatformAudio.Sink + ".monitor" }) start.ArgumentList.Add(arg);
        }
        else throw new PlatformNotSupportedException("Use the Windows edition on Windows.");
        var capture = new WavCapture(start, path);
        try { await capture.ready.Task.WaitAsync(TimeSpan.FromSeconds(15)); return capture; }
        catch { await capture.DisposeAsync(); throw new IOException(capture.diagnostic.Length > 0 ? capture.diagnostic : "Audio capture did not become ready. On macOS, allow Screen & System Audio Recording in Privacy & Security, then restart AssetRaider."); }
    }
    private async Task ReadErrors()
    {
        while (await process.StandardError.ReadLineAsync() is { } line)
        {
            if (line == "READY") ready.TrySetResult();
            else if (line.Length > 0) diagnostic = line.Length > 1500 ? line[..1500] : line;
        }
    }
    private async Task Pump()
    {
        try
        {
            var buffer = new byte[16384]; var retained = 0;
            while (true)
            {
                var count = await process.StandardOutput.BaseStream.ReadAsync(buffer.AsMemory(retained));
                if (count == 0) break;
                count += retained;
                var aligned = count - count % 4;
                if (aligned > 0)
                {
                    writer.Write(buffer, 0, aligned);
                    long loud = 0;
                    for (var i = 0; i < aligned; i += 2) if (Math.Abs((int)BinaryPrimitives.ReadInt16LittleEndian(buffer.AsSpan(i, 2))) > 8) loud++;
                    Interlocked.Add(ref audible, loud); Interlocked.Add(ref frames, aligned / 4);
                    if (OperatingSystem.IsLinux()) ready.TrySetResult();
                    if (Seconds > 1830) throw new IOException("Audio exceeded its 30-minute limit.");
                }
                retained = count - aligned;
                if (retained > 0) Buffer.BlockCopy(buffer, aligned, buffer, 0, retained);
            }
            await process.WaitForExitAsync(); await errors;
            if (!stopping || (process.ExitCode != 0 && !(OperatingSystem.IsLinux() && process.ExitCode == 130)))
                throw new IOException("Audio capture stopped unexpectedly. " + diagnostic);
            if (retained != 0) throw new IOException("Audio capture ended with an incomplete PCM frame.");
        }
        catch (Exception ex) { Fault = ex; ready.TrySetException(ex); }
        finally { if (!ready.Task.IsCompleted) ready.TrySetException(new IOException(diagnostic)); }
    }
    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        disposed = true; stopping = true;
        try
        {
            if (!process.HasExited)
            {
                if (OperatingSystem.IsMacOS()) { await process.StandardInput.WriteLineAsync("stop"); await process.StandardInput.FlushAsync(); }
                else kill(process.Id, 2);
            }
            await pump.WaitAsync(TimeSpan.FromSeconds(8));
        }
        catch (Exception ex)
        {
            Fault ??= new IOException("Capture could not stop cleanly; kept as partial.", ex);
            try { process.Kill(); } catch { }
            try { await pump.WaitAsync(TimeSpan.FromSeconds(3)); } catch { }
        }
        finally { try { writer.Dispose(); } finally { output.Dispose(); process.Dispose(); } }
    }
}

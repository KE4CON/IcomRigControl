using System.Diagnostics;

namespace IcomRigControl.Services;

/// <summary>
/// Linux / Raspberry Pi continuous audio output: pipe raw 16-bit mono PCM to
/// `aplay`'s stdin. aplay plays it gaplessly and applies natural back-pressure
/// (a Write blocks when its buffer is full), which paces the stream to real
/// time. Used on the Pi server to feed received TX audio into the radio, and on
/// a Linux client to play received RX audio. See CLAUDE.md Phase 12 milestone 3.
/// </summary>
public class LinuxStreamOutput : IAudioStreamOutput, IDisposable
{
    private Process? _process;
    private Stream? _stdin;

    public void Start(int sampleRateHz, string? deviceName = null)
    {
        Stop();
        string deviceArg = string.IsNullOrWhiteSpace(deviceName) ? "" : $"-D {deviceName} ";
        var psi = new ProcessStartInfo
        {
            FileName = "aplay",
            Arguments = $"{deviceArg}-f S16_LE -c 1 -r {sampleRateHz} -t raw -q",
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        _process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start aplay (is ALSA installed?).");
        _stdin = _process.StandardInput.BaseStream;
    }

    public void Write(short[] pcmFrame)
    {
        if (_stdin is null) return;
        var bytes = new byte[pcmFrame.Length * 2];
        Buffer.BlockCopy(pcmFrame, 0, bytes, 0, bytes.Length);
        try
        {
            _stdin.Write(bytes, 0, bytes.Length);
            _stdin.Flush();
        }
        catch
        {
            // aplay exited / pipe broke — Stop() will clean up.
        }
    }

    public void Stop()
    {
        try { _stdin?.Close(); } catch { }
        try { if (_process is { HasExited: false }) _process.Kill(); } catch { }
        _process?.Dispose();
        _process = null;
        _stdin = null;
    }

    public void Dispose() => Stop();
}

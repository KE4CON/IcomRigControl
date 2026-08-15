using System.Diagnostics;

namespace IcomRigControl.Services;

/// <summary>
/// macOS continuous audio output: pipe raw 16-bit little-endian mono PCM to SoX
/// `play`'s stdin. play renders it gaplessly and applies natural back-pressure (a
/// Write blocks when its buffer is full), pacing the stream to real time — the same
/// approach as LinuxStreamOutput's aplay. This lets a Mac client play remote RX
/// audio continuously, previously a no-op stub.
///
/// Requires SoX ("brew install sox"); if it isn't installed, Start throws a clear
/// message. See CLAUDE.md Phase 12 / macOS audio.
/// </summary>
public class MacStreamOutput : IAudioStreamOutput, IDisposable
{
    private Process? _process;
    private Stream? _stdin;

    public void Start(int sampleRateHz, string? deviceName = null)
    {
        Stop();
        var psi = new ProcessStartInfo
        {
            FileName = "play",
            Arguments = $"-q -c 1 -r {sampleRateHz} -b 16 -e signed-integer -L -t raw -",
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        if (!string.IsNullOrWhiteSpace(deviceName) && deviceName != "default")
            psi.Environment["AUDIODEV"] = deviceName;

        try
        {
            _process = Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start play.");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Could not start SoX 'play' for macOS audio output. Install SoX with 'brew install sox'.", ex);
        }
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
            // play exited / pipe broke — Stop() will clean up.
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

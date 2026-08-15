using System.Diagnostics;

namespace IcomRigControl.Services;

/// <summary>
/// macOS audio capture via SoX's `rec` (shelled out, the same "use an OS tool"
/// pattern as MacAudioPlayer's afplay and LinuxAudioCapture's arecord). Captures
/// 16-bit little-endian mono PCM at the requested rate from the default input (or a
/// named CoreAudio device via the AUDIODEV environment variable) and raises
/// SamplesCaptured per buffer. This is what lets a Mac CAPTURE receive audio for
/// CW/RTTY/HF-APRS decode and Remote Audio transmit — previously a no-op stub.
///
/// Requires SoX ("brew install sox"); if it isn't installed, Start throws a clear
/// message. See CLAUDE.md Phase 12 / macOS audio.
/// </summary>
public class MacAudioCapture : IAudioCapture, IDisposable
{
    private Process? _process;
    private CancellationTokenSource? _cts;
    private Task? _readTask;

    public bool IsCapturing { get; private set; }
    public event EventHandler<short[]>? SamplesCaptured;

    // macOS has no simple CLI to enumerate audio inputs; SoX uses the system default
    // or the AUDIODEV environment variable. Offer "default" plus a hint.
    public List<string> GetAvailableDevices() => new() { "default" };

    public void Start(int sampleRateHz, string? deviceName = null)
    {
        Stop();

        var psi = new ProcessStartInfo
        {
            FileName = "rec",
            // Output 16-bit signed little-endian mono raw PCM to stdout at the rate we want.
            Arguments = $"-q -c 1 -r {sampleRateHz} -b 16 -e signed-integer -L -t raw -",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        // SoX selects the capture device from the AUDIODEV environment variable.
        if (!string.IsNullOrWhiteSpace(deviceName) && deviceName != "default")
            psi.Environment["AUDIODEV"] = deviceName;

        try
        {
            _process = Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start rec.");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Could not start SoX 'rec' for macOS audio capture. Install SoX with 'brew install sox'.", ex);
        }

        IsCapturing = true;
        _cts = new CancellationTokenSource();
        _readTask = Task.Run(() => ReadLoopAsync(_cts.Token));
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        var stream = _process!.StandardOutput.BaseStream;
        var buffer = new byte[3200];
        bool haveCarry = false;
        byte carry = 0;

        while (!ct.IsCancellationRequested)
        {
            int offset = 0;
            if (haveCarry) { buffer[0] = carry; offset = 1; haveCarry = false; }

            int read;
            try { read = await stream.ReadAsync(buffer.AsMemory(offset), ct); }
            catch { break; }
            if (read <= 0) break;

            int total = read + offset;
            int sampleCount = total / 2;
            if (total % 2 == 1) { carry = buffer[total - 1]; haveCarry = true; } // keep the odd byte

            if (sampleCount > 0)
            {
                var samples = new short[sampleCount];
                Buffer.BlockCopy(buffer, 0, samples, 0, sampleCount * 2);
                SamplesCaptured?.Invoke(this, samples);
            }
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
        try
        {
            if (_process is { HasExited: false }) _process.Kill();
        }
        catch { /* already exited */ }
        _process?.Dispose();
        _process = null;
        _cts?.Dispose();
        _cts = null;
        _readTask = null;
        IsCapturing = false;
    }

    public void Dispose() => Stop();
}

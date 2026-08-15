using System.Diagnostics;
using System.Text.RegularExpressions;

namespace IcomRigControl.Services;

/// <summary>
/// Linux / Raspberry Pi audio capture via ALSA's `arecord` (shelled out, the
/// same "use the OS's own tool" pattern as MacAudioPlayer's afplay). Captures
/// 16-bit mono PCM at the requested rate and raises SamplesCaptured per buffer.
/// This is ESSENTIAL for the headless Pi server (it captures the radio's RX
/// audio to stream to a remote client). See CLAUDE.md Phase 12.
/// </summary>
public partial class LinuxAudioCapture : IAudioCapture, IDisposable
{
    private Process? _process;
    private CancellationTokenSource? _cts;
    private Task? _readTask;

    public bool IsCapturing { get; private set; }
    public event EventHandler<short[]>? SamplesCaptured;

    [GeneratedRegex(@"card (\d+):.*?device (\d+):")]
    private static partial Regex DeviceRegex();

    public List<string> GetAvailableDevices()
    {
        var devices = new List<string> { "default" };
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "arecord",
                Arguments = "-l",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p != null)
            {
                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                foreach (Match m in DeviceRegex().Matches(output))
                    devices.Add($"plughw:{m.Groups[1].Value},{m.Groups[2].Value}");
            }
        }
        catch
        {
            // arecord not installed / not Linux — just offer "default".
        }
        return devices;
    }

    public void Start(int sampleRateHz, string? deviceName = null)
    {
        Stop();

        string device = string.IsNullOrWhiteSpace(deviceName) ? "default" : deviceName;
        var psi = new ProcessStartInfo
        {
            FileName = "arecord",
            Arguments = $"-D {device} -f S16_LE -c 1 -r {sampleRateHz} -t raw -q",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        _process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start arecord (is ALSA installed?).");

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

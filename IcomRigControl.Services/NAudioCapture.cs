using NAudio.Wave;

namespace IcomRigControl.Services;

/// <summary>
/// Windows audio capture via NAudio (WaveIn). Captures 16-bit mono PCM at the
/// requested rate from an input device (the radio's USB audio for RX streaming,
/// or a microphone for TX) and raises SamplesCaptured per buffer. Windows
/// counterpart of IAudioPlayer's NAudioPlayer. See CLAUDE.md Phase 12.
/// </summary>
public class NAudioCapture : IAudioCapture, IDisposable
{
    private WaveInEvent? _waveIn;

    public bool IsCapturing { get; private set; }
    public event EventHandler<short[]>? SamplesCaptured;

    public List<string> GetAvailableDevices()
    {
        var devices = new List<string>();
        for (int i = 0; i < WaveInEvent.DeviceCount; i++)
            devices.Add(WaveInEvent.GetCapabilities(i).ProductName);
        return devices;
    }

    public void Start(int sampleRateHz, string? deviceName = null)
    {
        Stop();

        _waveIn = new WaveInEvent
        {
            DeviceNumber = FindDeviceNumber(deviceName),
            WaveFormat = new WaveFormat(sampleRateHz, bits: 16, channels: 1),
            BufferMilliseconds = 20
        };
        _waveIn.DataAvailable += OnDataAvailable;
        _waveIn.StartRecording();
        IsCapturing = true;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        int sampleCount = e.BytesRecorded / 2; // 16-bit
        var samples = new short[sampleCount];
        Buffer.BlockCopy(e.Buffer, 0, samples, 0, e.BytesRecorded);
        SamplesCaptured?.Invoke(this, samples);
    }

    private static int FindDeviceNumber(string? deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName)) return 0; // system default
        for (int i = 0; i < WaveInEvent.DeviceCount; i++)
            if (WaveInEvent.GetCapabilities(i).ProductName.Equals(deviceName, StringComparison.OrdinalIgnoreCase))
                return i;
        return 0;
    }

    public void Stop()
    {
        if (_waveIn != null)
        {
            _waveIn.DataAvailable -= OnDataAvailable;
            try { _waveIn.StopRecording(); } catch { /* never throw from Stop */ }
            _waveIn.Dispose();
            _waveIn = null;
        }
        IsCapturing = false;
    }

    public void Dispose() => Stop();
}

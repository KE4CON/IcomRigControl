using NAudio.Wave;

namespace IcomRigControl.Services;

/// <summary>
/// Windows continuous audio output for the remote-audio stream: a NAudio
/// BufferedWaveProvider fed by Write(), played by a WaveOutEvent. Over-full
/// buffers discard oldest audio (favor latency over backlog). See CLAUDE.md
/// Phase 12 milestone 3.
/// </summary>
public class NAudioStreamOutput : IAudioStreamOutput, IDisposable
{
    private WaveOutEvent? _waveOut;
    private BufferedWaveProvider? _buffer;

    public void Start(int sampleRateHz, string? deviceName = null)
    {
        // deviceName-based routing on Windows (WaveOut device number) is a future
        // refinement; for now WaveOutEvent uses the default output device.
        Stop();
        _buffer = new BufferedWaveProvider(new WaveFormat(sampleRateHz, bits: 16, channels: 1))
        {
            BufferDuration = TimeSpan.FromSeconds(2),
            DiscardOnBufferOverflow = true
        };
        _waveOut = new WaveOutEvent { DesiredLatency = 100 };
        _waveOut.Init(_buffer);
        _waveOut.Play();
    }

    public void Write(short[] pcmFrame)
    {
        if (_buffer is null) return;
        var bytes = new byte[pcmFrame.Length * 2];
        Buffer.BlockCopy(pcmFrame, 0, bytes, 0, bytes.Length);
        _buffer.AddSamples(bytes, 0, bytes.Length);
    }

    public void Stop()
    {
        try { _waveOut?.Stop(); } catch { }
        _waveOut?.Dispose();
        _waveOut = null;
        _buffer = null;
    }

    public void Dispose() => Stop();
}

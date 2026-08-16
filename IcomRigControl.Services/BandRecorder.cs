namespace IcomRigControl.Services;

/// <summary>
/// The Band DVR: captures the radio's receive audio into a rolling in-memory buffer
/// (so you can instantly "rewind" the last N seconds — catch a call you missed) and,
/// optionally, records continuously to a timestamped WAV file. Both run off the one
/// IAudioCapture. Resilient: capture errors are recorded, never thrown to the caller.
/// See CLAUDE.md Band DVR.
/// </summary>
public sealed class BandRecorder : IDisposable
{
    private readonly IAudioCapture _capture;
    private readonly int _sampleRate;
    private readonly object _lock = new();

    // Ring buffer holding the most recent `_ring.Length` samples.
    private readonly short[] _ring;
    private int _ringHead;   // next write position
    private int _ringCount;  // valid samples (<= capacity)

    private WavWriter? _writer;

    public bool IsCapturing { get; private set; }
    public bool IsRecording { get { lock (_lock) return _writer is not null; } }
    public string? CurrentFile { get { lock (_lock) return _writer?.Path; } }
    public int SampleRate => _sampleRate;
    public string? LastError { get; private set; }

    /// <param name="rollingSeconds">How many seconds of instant-replay history to keep.</param>
    public BandRecorder(IAudioCapture capture, int sampleRateHz = 44100, int rollingSeconds = 60)
    {
        _capture = capture;
        _sampleRate = sampleRateHz;
        _ring = new short[Math.Max(1, sampleRateHz * rollingSeconds)];
    }

    public void Start(string? deviceName = null)
    {
        Stop();
        _capture.SamplesCaptured += OnSamples;
        _capture.Start(_sampleRate, deviceName);
        IsCapturing = true;
    }

    public void Stop()
    {
        _capture.SamplesCaptured -= OnSamples;
        try { _capture.Stop(); } catch { }
        StopRecording();
        IsCapturing = false;
    }

    /// Begins recording to a timestamped WAV under the given directory; returns the path.
    public string StartRecording(string directory)
    {
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, $"Band_{DateTime.Now:yyyy-MM-dd_HHmmss}.wav");
        lock (_lock)
        {
            _writer?.Close();
            _writer = new WavWriter(path, _sampleRate);
        }
        return path;
    }

    public void StopRecording()
    {
        lock (_lock)
        {
            _writer?.Close();
            _writer = null;
        }
    }

    /// A snapshot of the last <paramref name="seconds"/> of received audio (or all
    /// that's buffered, if less) — for instant replay.
    public short[] GetRewind(int seconds)
    {
        int want = seconds * _sampleRate;
        lock (_lock)
        {
            int n = Math.Min(want, _ringCount);
            var outp = new short[n];
            // Oldest of the n samples starts (n) behind the head, modulo capacity.
            int start = (_ringHead - n + _ring.Length) % _ring.Length;
            for (int i = 0; i < n; i++)
                outp[i] = _ring[(start + i) % _ring.Length];
            return outp;
        }
    }

    private void OnSamples(object? sender, short[] samples)
    {
        try
        {
            lock (_lock)
            {
                foreach (short s in samples)
                {
                    _ring[_ringHead] = s;
                    _ringHead = (_ringHead + 1) % _ring.Length;
                    if (_ringCount < _ring.Length) _ringCount++;
                }
                _writer?.Write(samples);
            }
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
        }
    }

    public void Dispose() => Stop();
}

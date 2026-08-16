namespace IcomRigControl.Services;

/// <summary>
/// The Band DVR: captures the radio's receive audio into a rolling in-memory buffer
/// (so you can instantly "rewind" the last N seconds — catch a call you missed),
/// optionally records continuously to a timestamped WAV file, and can save a short
/// clip for a just-logged QSO. Audio is stored at a lower rate than it's captured
/// (default: capture 44.1 kHz, store 11.025 kHz) — plenty for SSB/CW/RTTY and 4x
/// smaller on disk. Resilient: capture errors are recorded, never thrown.
/// Implements IQsoAudioSource so the logger can attach per-contact audio.
/// See CLAUDE.md Band DVR.
/// </summary>
public sealed class BandRecorder : IQsoAudioSource, IDisposable
{
    private readonly IAudioCapture _capture;
    private readonly int _captureRate;
    private readonly int _storeRate;
    private readonly int _decimation; // captureRate / storeRate
    private readonly object _lock = new();

    private readonly short[] _ring; // most recent samples at the STORE rate
    private int _ringHead;
    private int _ringCount;
    private int _decimPhase; // running position within a decimation group

    private WavWriter? _writer;

    public bool IsCapturing { get; private set; }
    public bool IsRecording { get { lock (_lock) return _writer is not null; } }
    public string? CurrentFile { get { lock (_lock) return _writer?.Path; } }
    public int SampleRate => _storeRate;
    public string? LastError { get; private set; }

    /// Seconds of audio saved per QSO when the logger requests it.
    public int QsoClipSeconds { get; set; } = 60;

    public static string RecordingsDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "IcomRigControl", "Recordings");
    public static string QsoAudioDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "IcomRigControl", "QsoAudio");

    public BandRecorder(IAudioCapture capture, int captureRateHz = 44100, int storeRateHz = 11025,
        int rollingSeconds = 60)
    {
        _capture = capture;
        _captureRate = captureRateHz;
        _storeRate = storeRateHz <= 0 ? captureRateHz : storeRateHz;
        _decimation = Math.Max(1, _captureRate / _storeRate);
        _ring = new short[Math.Max(1, _storeRate * rollingSeconds)];
    }

    public void Start(string? deviceName = null)
    {
        Stop();
        _capture.SamplesCaptured += OnSamples;
        _capture.Start(_captureRate, deviceName);
        IsCapturing = true;
    }

    public void Stop()
    {
        _capture.SamplesCaptured -= OnSamples;
        try { _capture.Stop(); } catch { }
        StopRecording();
        IsCapturing = false;
    }

    public string StartRecording(string directory)
    {
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, $"Band_{DateTime.Now:yyyy-MM-dd_HHmmss}.wav");
        lock (_lock)
        {
            _writer?.Close();
            _writer = new WavWriter(path, _storeRate);
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

    /// A snapshot of the last <paramref name="seconds"/> of received audio (store rate).
    public short[] GetRewind(int seconds)
    {
        int want = seconds * _storeRate;
        lock (_lock)
        {
            int n = Math.Min(want, _ringCount);
            var outp = new short[n];
            int start = (_ringHead - n + _ring.Length) % _ring.Length;
            for (int i = 0; i < n; i++) outp[i] = _ring[(start + i) % _ring.Length];
            return outp;
        }
    }

    /// IQsoAudioSource: saves the last QsoClipSeconds to a WAV named for the callsign,
    /// or null if not currently monitoring / nothing buffered.
    public string? SaveQsoAudio(string callsign)
    {
        if (!IsCapturing) return null;
        short[] pcm = GetRewind(QsoClipSeconds);
        if (pcm.Length == 0) return null;

        Directory.CreateDirectory(QsoAudioDir);
        string safe = new string((callsign ?? "QSO").Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '/').ToArray());
        if (safe.Length == 0) safe = "QSO";
        string path = Path.Combine(QsoAudioDir, $"QSO_{safe}_{DateTime.Now:yyyy-MM-dd_HHmmss}.wav");
        WavWriter.WriteFile(path, pcm, _storeRate);
        return path;
    }

    private void OnSamples(object? sender, short[] samples)
    {
        try
        {
            lock (_lock)
            {
                // Downsample captureRate -> storeRate by keeping every _decimation'th sample.
                var outBuf = new short[samples.Length / _decimation + 1];
                int outN = 0;
                for (int i = 0; i < samples.Length; i++)
                {
                    if (_decimPhase++ < _decimation - 1) continue;
                    _decimPhase = 0;
                    outBuf[outN++] = samples[i];
                }

                for (int k = 0; k < outN; k++)
                {
                    _ring[_ringHead] = outBuf[k];
                    _ringHead = (_ringHead + 1) % _ring.Length;
                    if (_ringCount < _ring.Length) _ringCount++;
                }
                if (outN > 0 && _writer is not null)
                {
                    var w = new short[outN];
                    Array.Copy(outBuf, w, outN);
                    _writer.Write(w);
                }
            }
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
        }
    }

    public void Dispose() => Stop();
}

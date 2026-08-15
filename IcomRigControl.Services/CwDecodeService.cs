using IcomRigControl.CivEngine;

namespace IcomRigControl.Services;

/// <summary>
/// Live CW reader: runs an IAudioCapture on the radio's receive audio and decodes
/// Morse in real time. Audio chunks are fed straight through a streaming
/// CwToneDetector (audio -&gt; keyed segments) and MorseDecoder (segments -&gt; text);
/// decoded characters are raised as they are recognized. In parallel it measures the
/// received tone's actual pitch (CwPitchMeter) so the UI can show how far off the
/// signal is and offer a one-touch "zero beat". Resilient: a bad chunk is caught and
/// recorded, never thrown to the capture thread. See CLAUDE.md CW decode.
/// </summary>
public class CwDecodeService
{
    private readonly IAudioCapture _capture;
    private readonly double _pitchHz;
    private readonly int _sampleRate;
    private readonly object _lock = new();

    private CwToneDetector? _detector;
    private MorseDecoder? _decoder;

    private readonly List<float> _pitchBuffer = new();
    private double? _lastToneHz;

    public bool IsReceiving { get; private set; }
    public string? LastError { get; private set; }

    /// The target CW pitch the reader is tuned to (Hz) — zero beat aims to land the
    /// received tone exactly here.
    public double PitchHz => _pitchHz;

    /// The most recently measured actual tone frequency (Hz), or null if no clear
    /// tone has been heard yet. Used by the "Zero Beat" action.
    public double? LastToneHz { get { lock (_lock) return _lastToneHz; } }

    /// Newly decoded text (characters, and spaces between words).
    public event EventHandler<string>? TextDecoded;

    /// The current estimated sender speed, in words per minute.
    public event EventHandler<double>? WpmChanged;

    /// A fresh tone measurement: the actual received pitch (Hz) and its error from
    /// the target pitch (measured - target). Raised only while a tone is present.
    public event EventHandler<CwToneReading>? ToneMeasured;

    public CwDecodeService(IAudioCapture capture, double pitchHz = 600, int sampleRateHz = 44100)
    {
        _capture = capture;
        _pitchHz = pitchHz;
        _sampleRate = sampleRateHz;
    }

    public void Start(string? deviceName = null)
    {
        Stop();
        lock (_lock)
        {
            _detector = new CwToneDetector(_pitchHz, _sampleRate);
            _decoder = new MorseDecoder();
            _pitchBuffer.Clear();
            _lastToneHz = null;
        }
        _capture.SamplesCaptured += OnSamplesCaptured;
        _capture.Start(_sampleRate, deviceName);
        IsReceiving = true;
    }

    private void OnSamplesCaptured(object? sender, short[] samples)
    {
        try
        {
            var audio = new float[samples.Length];
            for (int i = 0; i < samples.Length; i++) audio[i] = samples[i] / 32768f;

            string decoded = "";
            double wpm;
            lock (_lock)
            {
                if (_detector is null || _decoder is null) return;
                foreach (var seg in _detector.Feed(audio))
                    decoded += _decoder.Add(seg.On, seg.Seconds);
                wpm = _decoder.EstimatedWpm;

                MeasureTone(audio);
            }

            if (decoded.Length > 0)
            {
                TextDecoded?.Invoke(this, decoded);
                WpmChanged?.Invoke(this, wpm);
            }
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
        }
    }

    // Keeps a short rolling window of audio and, when it's full enough, measures the
    // dominant tone. Only publishes a reading when a clear tone is present (so the
    // tuning display and zero-beat never lock onto silence/noise). Caller holds _lock.
    private void MeasureTone(float[] audio)
    {
        _pitchBuffer.AddRange(audio);
        int window = _sampleRate / 5; // ~200 ms

        // Measure every complete window in the buffer (a large capture chunk holds
        // several), so a tone anywhere in the chunk is seen — not just the tail.
        int consumed = 0;
        while (_pitchBuffer.Count - consumed >= window)
        {
            float[] slice = _pitchBuffer.GetRange(consumed, window).ToArray();
            consumed += window;

            double? tone = CwPitchMeter.MeasureToneHz(slice, _sampleRate, _pitchHz, spanHz: 300);
            if (tone is { } t)
            {
                _lastToneHz = t;
                ToneMeasured?.Invoke(this, new CwToneReading(t, t - _pitchHz));
            }
        }
        if (consumed > 0) _pitchBuffer.RemoveRange(0, consumed);
    }

    public void Stop()
    {
        _capture.SamplesCaptured -= OnSamplesCaptured;
        try { _capture.Stop(); } catch { }

        string tail = "";
        lock (_lock)
        {
            if (_detector?.Flush() is { } last && _decoder is not null)
                tail += _decoder.Add(last.On, last.Seconds);
            if (_decoder is not null) tail += _decoder.Flush();
            _detector = null;
            _decoder = null;
        }
        if (tail.Length > 0) TextDecoded?.Invoke(this, tail);
        IsReceiving = false;
    }
}

/// A single tone measurement: the actual received pitch and its error from target.
public readonly record struct CwToneReading(double ToneHz, double ErrorHz);

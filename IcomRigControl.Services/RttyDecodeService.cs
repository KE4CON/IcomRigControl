using IcomRigControl.CivEngine;

namespace IcomRigControl.Services;

/// <summary>
/// Live RTTY reader: runs an IAudioCapture on the radio's receive audio and feeds it
/// straight through a streaming RttyDecoder (FSK demod + async Baudot framing),
/// raising decoded text as it arrives. "Reverse" swaps mark/space for the wrong-
/// sideband case and can be flipped while running. BeginAutoDetect() is the "Auto"
/// action: it collects a few seconds of audio, runs RttyAutoDetector on it off the
/// capture thread, and switches the live decoder to whichever shift/baud it found —
/// decoding keeps running on the old profile while that measurement is in flight, so
/// there's no gap. Resilient: a bad chunk is caught and recorded, never thrown to the
/// capture thread. See CLAUDE.md RTTY decode.
/// </summary>
public class RttyDecodeService
{
    private readonly IAudioCapture _capture;
    private readonly int _sampleRate;
    private readonly bool _unshiftOnSpace;
    private readonly object _lock = new();

    private RttyDecoder? _decoder;
    private bool _reverse;

    private readonly List<float> _autoBuffer = new();
    private bool _autoDetecting;
    private int _autoDetectSamplesNeeded;

    public bool IsReceiving { get; private set; }
    public string? LastError { get; private set; }

    /// The shift/baud currently in use — RttyProfile.Hf45Baud until BeginAutoDetect()
    /// finds something else.
    public RttyProfile Profile { get; private set; }

    public event EventHandler<string>? TextDecoded;

    /// Raised once a BeginAutoDetect() measurement completes and the decoder has
    /// switched to the detected profile.
    public event EventHandler<RttyProfile>? AutoDetected;

    public RttyDecodeService(IAudioCapture capture, RttyProfile? profile = null,
                             int sampleRateHz = 44100, bool reverse = false, bool unshiftOnSpace = true)
    {
        _capture = capture;
        Profile = profile ?? RttyProfile.Hf45Baud;
        _sampleRate = sampleRateHz;
        _reverse = reverse;
        _unshiftOnSpace = unshiftOnSpace;
    }

    public void Start(string? deviceName = null)
    {
        Stop();
        lock (_lock) _decoder = new RttyDecoder(Profile, _sampleRate, _reverse, _unshiftOnSpace);
        _capture.SamplesCaptured += OnSamplesCaptured;
        _capture.Start(_sampleRate, deviceName);
        IsReceiving = true;
    }

    /// Flips mark/space polarity. Resets the decoder (a fresh sync), so it takes
    /// effect on the next received characters.
    public void SetReverse(bool reverse)
    {
        lock (_lock)
        {
            _reverse = reverse;
            if (_decoder is not null) _decoder = new RttyDecoder(Profile, _sampleRate, _reverse, _unshiftOnSpace);
        }
    }

    /// Starts a one-shot "Auto" measurement: collects `seconds` of the next captured
    /// audio, then (off the capture thread) tries RttyAutoDetector's shift/baud grid
    /// against it and switches the live decoder to the best match, raising
    /// AutoDetected. Needs a real, steady RTTY signal in the passband while it collects.
    public void BeginAutoDetect(double seconds = 3.0)
    {
        lock (_lock)
        {
            _autoBuffer.Clear();
            _autoDetectSamplesNeeded = (int)(seconds * _sampleRate);
            _autoDetecting = true;
        }
    }

    private void OnSamplesCaptured(object? sender, short[] samples)
    {
        try
        {
            var audio = new float[samples.Length];
            for (int i = 0; i < samples.Length; i++) audio[i] = samples[i] / 32768f;

            float[]? autoSnapshot = null;
            lock (_lock)
            {
                if (_autoDetecting)
                {
                    _autoBuffer.AddRange(audio);
                    if (_autoBuffer.Count >= _autoDetectSamplesNeeded)
                    {
                        autoSnapshot = _autoBuffer.ToArray();
                        _autoBuffer.Clear();
                        _autoDetecting = false;
                    }
                }
            }
            // Runs the candidate-grid search off the capture thread so a live stream
            // of audio chunks never stalls behind it.
            if (autoSnapshot is not null) _ = Task.Run(() => RunAutoDetect(autoSnapshot));

            string decoded;
            lock (_lock)
            {
                if (_decoder is null) return;
                decoded = _decoder.Feed(audio);
            }
            if (decoded.Length > 0) TextDecoded?.Invoke(this, decoded);
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
        }
    }

    private void RunAutoDetect(float[] audio)
    {
        try
        {
            var result = RttyAutoDetector.Detect(audio, _sampleRate);
            lock (_lock)
            {
                Profile = result.Profile;
                if (_decoder is not null) _decoder = new RttyDecoder(Profile, _sampleRate, _reverse, _unshiftOnSpace);
            }
            AutoDetected?.Invoke(this, result.Profile);
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
        }
    }

    public void Stop()
    {
        _capture.SamplesCaptured -= OnSamplesCaptured;
        try { _capture.Stop(); } catch { }
        lock (_lock) _decoder = null;
        IsReceiving = false;
    }
}

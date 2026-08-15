using IcomRigControl.CivEngine;

namespace IcomRigControl.Services;

/// <summary>
/// Live RTTY reader: runs an IAudioCapture on the radio's receive audio and feeds it
/// straight through a streaming RttyDecoder (FSK demod + async Baudot framing),
/// raising decoded text as it arrives. "Reverse" swaps mark/space for the wrong-
/// sideband case and can be flipped while running. Resilient: a bad chunk is caught
/// and recorded, never thrown to the capture thread. See CLAUDE.md RTTY decode.
/// </summary>
public class RttyDecodeService
{
    private readonly IAudioCapture _capture;
    private readonly RttyProfile _profile;
    private readonly int _sampleRate;
    private readonly bool _unshiftOnSpace;
    private readonly object _lock = new();

    private RttyDecoder? _decoder;
    private bool _reverse;

    public bool IsReceiving { get; private set; }
    public string? LastError { get; private set; }

    public event EventHandler<string>? TextDecoded;

    public RttyDecodeService(IAudioCapture capture, RttyProfile? profile = null,
                             int sampleRateHz = 44100, bool reverse = false, bool unshiftOnSpace = true)
    {
        _capture = capture;
        _profile = profile ?? RttyProfile.Hf45Baud;
        _sampleRate = sampleRateHz;
        _reverse = reverse;
        _unshiftOnSpace = unshiftOnSpace;
    }

    public void Start(string? deviceName = null)
    {
        Stop();
        lock (_lock) _decoder = new RttyDecoder(_profile, _sampleRate, _reverse, _unshiftOnSpace);
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
            if (_decoder is not null) _decoder = new RttyDecoder(_profile, _sampleRate, _reverse, _unshiftOnSpace);
        }
    }

    private void OnSamplesCaptured(object? sender, short[] samples)
    {
        try
        {
            var audio = new float[samples.Length];
            for (int i = 0; i < samples.Length; i++) audio[i] = samples[i] / 32768f;

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

    public void Stop()
    {
        _capture.SamplesCaptured -= OnSamplesCaptured;
        try { _capture.Stop(); } catch { }
        lock (_lock) _decoder = null;
        IsReceiving = false;
    }
}

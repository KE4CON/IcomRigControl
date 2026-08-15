using IcomRigControl.CivEngine;

namespace IcomRigControl.Services;

/// <summary>
/// Live HF APRS receiver: runs an IAudioCapture on the radio's RX audio, buffers
/// it, and periodically decodes it (AprsReceiver — demod/deframe/AX.25/parse with
/// bit-sync), raising PacketReceived for each newly heard packet. Resilient: decode
/// errors are recorded, never thrown to the caller. Overlapping windows plus a
/// short content-dedup catch packets that straddle a window boundary without
/// double-reporting. See CLAUDE.md HF APRS.
/// </summary>
public class AprsReceiveService
{
    private readonly IAudioCapture _capture;
    private readonly AfskProfile _profile;
    private readonly int _sampleRate;
    private readonly int _processIntervalMs;

    private readonly List<float> _buffer = new();
    private readonly object _bufferLock = new();
    private readonly Dictionary<string, DateTime> _recent = new();

    private CancellationTokenSource? _cts;
    private Task? _processTask;

    public bool IsReceiving { get; private set; }
    public string? LastError { get; private set; }

    public event EventHandler<AprsReception>? PacketReceived;

    public AprsReceiveService(IAudioCapture capture, AfskProfile? profile = null,
                              int sampleRateHz = 44100, int processIntervalMs = 2000)
    {
        _capture = capture;
        _profile = profile ?? AfskProfile.Hf300Baud;
        _sampleRate = sampleRateHz;
        _processIntervalMs = processIntervalMs;
    }

    public void Start(string? deviceName = null)
    {
        Stop();
        _capture.SamplesCaptured += OnSamplesCaptured;
        _capture.Start(_sampleRate, deviceName);
        _cts = new CancellationTokenSource();
        _processTask = Task.Run(() => ProcessLoopAsync(_cts.Token));
        IsReceiving = true;
    }

    private void OnSamplesCaptured(object? sender, short[] samples)
    {
        lock (_bufferLock)
        {
            foreach (short s in samples) _buffer.Add(s / 32768f);

            // Cap at ~10s so a stalled processor can't grow the buffer without bound.
            int max = _sampleRate * 10;
            if (_buffer.Count > max) _buffer.RemoveRange(0, _buffer.Count - max);
        }
    }

    private async Task ProcessLoopAsync(CancellationToken ct)
    {
        int overlapTail = _sampleRate; // keep ~1s so a boundary-straddling packet is caught next round

        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(_processIntervalMs, ct); }
            catch (OperationCanceledException) { break; }

            float[] snapshot;
            lock (_bufferLock)
            {
                if (_buffer.Count < _sampleRate / 2) continue; // wait for enough audio
                snapshot = _buffer.ToArray();
                int keep = Math.Min(_buffer.Count, overlapTail);
                _buffer.RemoveRange(0, _buffer.Count - keep);
            }

            try
            {
                foreach (var reception in AprsReceiver.DecodePackets(snapshot, _profile, _sampleRate))
                {
                    if (IsDuplicate(reception)) continue;
                    PacketReceived?.Invoke(this, reception);
                }
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
            }
        }
    }

    private bool IsDuplicate(AprsReception reception)
    {
        string key = $"{reception.Frame.Source}-{reception.Frame.SourceSsid}|{reception.Frame.InfoField}";
        var now = DateTime.UtcNow;
        lock (_bufferLock)
        {
            // Prune stale entries so the dictionary can't grow forever.
            if (_recent.Count > 500)
                foreach (var k in _recent.Where(kv => now - kv.Value > TimeSpan.FromMinutes(5)).Select(kv => kv.Key).ToList())
                    _recent.Remove(k);

            if (_recent.TryGetValue(key, out var last) && now - last < TimeSpan.FromSeconds(15))
                return true; // same packet from an overlapping window
            _recent[key] = now;
            return false;
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
        _capture.SamplesCaptured -= OnSamplesCaptured;
        try { _capture.Stop(); } catch { }
        _cts?.Dispose();
        _cts = null;
        _processTask = null;
        IsReceiving = false;
        lock (_bufferLock) { _buffer.Clear(); _recent.Clear(); }
    }
}

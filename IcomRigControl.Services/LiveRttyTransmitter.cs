using System.Collections.Concurrent;
using IcomRigControl.CivEngine;
using IcomRigControl.RigModel;

namespace IcomRigControl.Services;

/// <summary>
/// Live "keyboard" RTTY transmit: keeps the radio keyed and continuously streams
/// RTTY audio, sending characters the instant you type them and idle tone (the RTTY
/// rest state) in between — a real ragchew-style TTY terminal, versus sending a whole
/// buffer at once. Keying honors TransmitInhibited and PTT is always released on stop.
/// The audio goes to the radio's transmit input via IAudioStreamOutput (continuous,
/// back-pressured). See CLAUDE.md live RTTY TX.
/// </summary>
public sealed class LiveRttyTransmitter
{
    private readonly Transceiver _rig;
    private readonly IAudioStreamOutput _output;
    private readonly RttyProfile _profile;
    private readonly int _rate;
    private readonly ConcurrentQueue<string> _queue = new();

    private CancellationTokenSource? _cts;

    public bool IsTransmitting { get; private set; }

    public LiveRttyTransmitter(Transceiver rig, IAudioStreamOutput output,
        RttyProfile? profile = null, int sampleRateHz = 44100)
    {
        _rig = rig;
        _output = output;
        _profile = profile ?? RttyProfile.Hf45Baud;
        _rate = sampleRateHz;
    }

    /// Keys the radio and starts streaming (idle until you type). Returns false if
    /// transmit is inhibited.
    public async Task<bool> StartAsync(string? deviceName = null)
    {
        if (IsTransmitting) return true;
        if (_rig.TransmitInhibited) return false;

        await _rig.SetPttAsync(true);
        _output.Start(_rate, deviceName);
        IsTransmitting = true;
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        _ = Task.Run(() => { while (!ct.IsCancellationRequested) PumpOnce(); });
        return true;
    }

    /// Queues text to be transmitted as soon as possible.
    public void Enqueue(string text)
    {
        if (!string.IsNullOrEmpty(text)) _queue.Enqueue(text);
    }

    /// Stops transmitting and releases PTT — always.
    public async Task StopAsync()
    {
        _cts?.Cancel();
        _cts = null;
        try { _output.Stop(); } catch { }
        IsTransmitting = false;
        try { await _rig.SetPttAsync(false); } catch { }
    }

    // One streaming step: send the next queued text, or an idle chunk if there's none.
    // Back-pressure in the stream output paces this to real time.
    private void PumpOnce()
    {
        _queue.TryDequeue(out string? text);
        short[] chunk = BuildChunk(text, _profile, _rate);
        try { _output.Write(chunk); } catch { }
    }

    /// Builds one PCM chunk: the modulated text, or ~1/3 s of idle tone if null/empty.
    /// Pure — no radio, no output — so it's unit-testable.
    public static short[] BuildChunk(string? text, RttyProfile profile, int sampleRateHz)
    {
        float[] audio = string.IsNullOrEmpty(text)
            ? RttyModulator.Modulate("", profile, sampleRateHz, leadIdleBits: 16) // idle (rest) tone
            : RttyModulator.Modulate(text, profile, sampleRateHz, leadIdleBits: 0);

        var pcm = new short[audio.Length];
        for (int i = 0; i < audio.Length; i++) pcm[i] = (short)(audio[i] * 32767);
        return pcm;
    }
}

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
    private Task? _pumpTask;

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
        _pumpTask = Task.Run(() => PumpLoopAsync(_cts.Token));
        return true;
    }

    /// Queues text to be transmitted as soon as possible.
    public void Enqueue(string text)
    {
        if (!string.IsNullOrEmpty(text)) _queue.Enqueue(text);
    }

    /// Stops transmitting and releases PTT — always. Awaits the pump so no late Write
    /// lands on a stopped output, and disposes the CTS.
    public async Task StopAsync()
    {
        var cts = _cts;
        _cts = null;
        cts?.Cancel();

        var task = _pumpTask;
        _pumpTask = null;
        if (task is not null) { try { await task; } catch { } } // wait for the pump to exit

        try { _output.Stop(); } catch { }
        cts?.Dispose();
        IsTransmitting = false;
        try { await _rig.SetPttAsync(false); } catch { } // unkey last — always
    }

    // Streams the next queued text, or an idle chunk, then paces itself to the chunk's
    // real playback duration. This is what makes it correct on Windows too — the
    // NAudio stream output does NOT block, so without this delay the loop would spin a
    // CPU core and continuously overflow (discard) the output buffer, garbling TX.
    private async Task PumpLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            _queue.TryDequeue(out string? text);
            short[] chunk = BuildChunk(text, _profile, _rate);
            try { _output.Write(chunk); } catch { }

            int ms = chunk.Length * 1000 / _rate; // real-time duration of this chunk
            try { await Task.Delay(Math.Max(1, ms), ct); } catch { break; }
        }
    }

    /// Builds one PCM chunk: the modulated text, or ~1/3 s of idle tone if null/empty.
    /// Pure — no radio, no output — so it's unit-testable.
    public static short[] BuildChunk(string? text, RttyProfile profile, int sampleRateHz)
    {
        float[] audio = string.IsNullOrEmpty(text)
            ? RttyModulator.Modulate("", profile, sampleRateHz, leadIdleBits: 8) // short idle (rest) tone — keeps typed-char latency low
            : RttyModulator.Modulate(text, profile, sampleRateHz, leadIdleBits: 0);

        var pcm = new short[audio.Length];
        for (int i = 0; i < audio.Length; i++) pcm[i] = (short)(audio[i] * 32767);
        return pcm;
    }
}

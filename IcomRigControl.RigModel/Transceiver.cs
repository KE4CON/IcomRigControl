using IcomRigControl.CivEngine;

namespace IcomRigControl.RigModel;

/// <summary>
/// High-level radio model. Owns the transport and CI-V engine pieces,
/// exposes clean async methods and events. This is the only class that
/// Services and UI should ever talk to directly.
/// </summary>
public class Transceiver : IAsyncDisposable
{
    private readonly ICivTransport _transport;
    private readonly CivFrameBuilder _builder;
    private readonly CivFrameParser _parser;
    private readonly byte _radioAddress;

    private CancellationTokenSource? _pollCts;
    private Task? _pollTask;

    private TaskCompletionSource<long>? _pendingFreqResponse;
    private TaskCompletionSource<string>? _pendingModeResponse;

    private CancellationTokenSource? _scopeCts;
    private Task? _scopeTask;

    public bool IsConnected { get; private set; }
    public long FrequencyHz { get; private set; }
    public string Mode { get; private set; } = string.Empty;
    public bool PttActive { get; private set; }

    public double SMeterDbm { get; private set; }
    public int SMeterS { get; private set; }
    public double RfPowerPercent { get; private set; }
    public double SwrRatio { get; private set; } = 1.0;
    public double AlcLevel { get; private set; }
    public double SupplyVoltage { get; private set; }
    public double CurrentDraw { get; private set; }

    public bool IsScopeRunning { get; private set; }

    public long CurrentSpanHz { get; private set; } = 50_000;
    public int[] LastWaveform { get; private set; } = Array.Empty<int>();

    public event EventHandler<MeterSnapshot>? MeterUpdated;
    public event EventHandler<long>? FrequencyChanged;
    public event EventHandler<string>? ModeChanged;
    public event EventHandler<bool>? PttChanged;
    public event EventHandler<int[]>? WaveformUpdated;

    public Transceiver(ICivTransport transport, RadioModel model)
    {
        _transport = transport;
        _radioAddress = model == RadioModel.IC7300MK2
            ? CivCommands.Addr7300Mk2
            : CivCommands.Addr7300;
        _builder = new CivFrameBuilder(_radioAddress);
        _parser = new CivFrameParser();

        _transport.DataReceived += OnDataReceived;
    }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        await _transport.OpenAsync(ct);
        IsConnected = _transport.IsOpen;
    }

    public async Task DisconnectAsync()
    {
        StopPolling();
        StopScope();
        await _transport.CloseAsync();
        IsConnected = false;
    }

    public async Task SetFrequencyAsync(long hz, CancellationToken ct = default)
    {
        var frame = _builder.SetFrequency(hz);
        await _transport.WriteAsync(frame, ct);
        FrequencyHz = hz;
        FrequencyChanged?.Invoke(this, hz);
    }

    public async Task SetPttAsync(bool transmit, CancellationToken ct = default)
    {
        var frame = _builder.SetPtt(transmit);
        await _transport.WriteAsync(frame, ct);
        PttActive = transmit;
        PttChanged?.Invoke(this, transmit);
    }

    /// Read a single memory channel's frequency and mode by selecting it,
    /// then requesting frequency and mode reads while it's active.
    /// Returns null if no response arrives within the timeout (empty channel).
    public async Task<MemoryChannel?> ReadMemoryChannelAsync(int channelNumber, CancellationToken ct = default)
    {
        await _transport.WriteAsync(_builder.SelectMemoryChannel(channelNumber), ct);
        await Task.Delay(50, ct);

        _pendingFreqResponse = new TaskCompletionSource<long>();
        await _transport.WriteAsync(_builder.ReadFrequency(), ct);
        var (freqOk, capturedFreq) = await WaitWithTimeout(_pendingFreqResponse.Task, 500, ct);
        _pendingFreqResponse = null;

        if (!freqOk)
        {
            return null;
        }

        _pendingModeResponse = new TaskCompletionSource<string>();
        await _transport.WriteAsync(_builder.ReadMode(), ct);
        var (modeOk, capturedMode) = await WaitWithTimeout(_pendingModeResponse.Task, 500, ct);
        _pendingModeResponse = null;

        return new MemoryChannel(channelNumber, capturedFreq, modeOk ? capturedMode : "USB", string.Empty);
    }

    private static async Task<(bool success, T value)> WaitWithTimeout<T>(Task<T> task, int timeoutMs, CancellationToken ct)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var delayTask = Task.Delay(timeoutMs, timeoutCts.Token);
        var completed = await Task.WhenAny(task, delayTask);

        if (completed == task)
        {
            timeoutCts.Cancel();
            return (true, await task);
        }

        return (false, default!);
    }

    /// Read all channels 1-99, skipping any that don't respond (empty channels).
    /// Reports progress via the optional callback (channel number just completed, total).
    public async Task<List<MemoryChannel>> ReadAllMemoriesAsync(
        IProgress<(int current, int total)>? progress = null,
        CancellationToken ct = default)
    {
        var results = new List<MemoryChannel>();
        const int totalChannels = 99;

        for (int ch = 1; ch <= totalChannels; ch++)
        {
            ct.ThrowIfCancellationRequested();
            var channel = await ReadMemoryChannelAsync(ch, ct);
            if (channel != null)
            {
                results.Add(channel);
            }
            progress?.Report((ch, totalChannels));
        }

        return results;
    }

    /// Write a single memory channel: select it, then set frequency and mode.
    public async Task WriteMemoryChannelAsync(MemoryChannel channel, CancellationToken ct = default)
    {
        await _transport.WriteAsync(_builder.SelectMemoryChannel(channel.ChannelNumber), ct);
        await Task.Delay(50, ct);

        await SetFrequencyAsync(channel.FrequencyHz, ct);
        await Task.Delay(50, ct);

        await SetModeAsync(channel.Mode, ct);
        await Task.Delay(50, ct);
    }

    public async Task SetModeAsync(string mode, CancellationToken ct = default)
    {
        byte modeCode = StringToModeCode(mode);
        var frame = _builder.SetMode(modeCode);
        await _transport.WriteAsync(frame, ct);
        Mode = mode;
        ModeChanged?.Invoke(this, mode);
    }

    private static byte StringToModeCode(string mode) => mode switch
    {
        "LSB" => 0x00,
        "USB" => 0x01,
        "AM" => 0x02,
        "CW" => 0x03,
        "RTTY" => 0x04,
        "FM" => 0x05,
        "CW-R" => 0x07,
        "RTTY-R" => 0x08,
        "DV" => 0x17,
        _ => 0x01
    };

    private static string ModeCodeToString(byte code) => code switch
    {
        0x00 => "LSB",
        0x01 => "USB",
        0x02 => "AM",
        0x03 => "CW",
        0x04 => "RTTY",
        0x05 => "FM",
        0x07 => "CW-R",
        0x08 => "RTTY-R",
        0x17 => "DV",
        _ => "UNKNOWN"
    };

    /// Start a background loop that polls all meters at the given interval.
    public void StartPolling(TimeSpan interval)
    {
        StopPolling();
        _pollCts = new CancellationTokenSource();
        _pollTask = Task.Run(() => PollLoopAsync(interval, _pollCts.Token));
    }

    public void StopPolling()
    {
        _pollCts?.Cancel();
        _pollCts = null;
    }

    private async Task PollLoopAsync(TimeSpan interval, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await PollOnceAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Swallow transient poll errors (per CLAUDE.md: log, don't crash the loop).
            }

            try
            {
                await Task.Delay(interval, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task PollOnceAsync(CancellationToken ct)
    {
        await _transport.WriteAsync(_builder.ReadSMeter(), ct);
        await _transport.WriteAsync(_builder.ReadPower(), ct);
        await _transport.WriteAsync(_builder.ReadSwr(), ct);
        await _transport.WriteAsync(_builder.ReadAlc(), ct);
        await _transport.WriteAsync(_builder.ReadVoltage(), ct);
        await _transport.WriteAsync(_builder.ReadCurrent(), ct);

        var snapshot = new MeterSnapshot(
            DateTimeOffset.UtcNow,
            FrequencyHz,
            Mode,
            SMeterDbm,
            SMeterS,
            RfPowerPercent,
            SwrRatio,
            AlcLevel,
            SupplyVoltage,
            CurrentDraw
        );
        MeterUpdated?.Invoke(this, snapshot);
    }
    /// Start the spectrum scope: turns it on, enables waveform output, sets
    /// the requested span, and begins a background loop requesting a new
    /// sweep at the given interval. CurrentSpanHz is updated so the UI can
    /// compute accurate frequency axis labels and click-to-tune positions.
    public async Task StartScopeAsync(TimeSpan sweepInterval, long spanHz = 50_000, CancellationToken ct = default)
    {
        StopScope();

        await _transport.WriteAsync(_builder.SetScopeOn(true), ct);
        await Task.Delay(50, ct);
        await _transport.WriteAsync(_builder.SetWaveformOutput(true), ct);
        await Task.Delay(50, ct);
        await _transport.WriteAsync(_builder.SetScopeSpan(spanHz), ct);
        await Task.Delay(50, ct);

        CurrentSpanHz = spanHz;
        IsScopeRunning = true;
        _scopeCts = new CancellationTokenSource();
        _scopeTask = Task.Run(() => ScopeLoopAsync(sweepInterval, _scopeCts.Token));
    }
    

    

    public void StopScope()
    {
        _scopeCts?.Cancel();
        _scopeCts = null;
        IsScopeRunning = false;
    }

    private async Task ScopeLoopAsync(TimeSpan interval, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await _transport.WriteAsync(_builder.ReadWaveformData(), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Swallow transient scope read errors — same pattern as meter polling.
            }

            try
            {
                await Task.Delay(interval, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void OnDataReceived(object? sender, byte[] data)
    {
        var frames = _parser.Feed(data);
        foreach (var frame in frames)
        {
            ApplyFrame(frame);
        }
    }

    private void ApplyFrame(CivFrame frame)
    {
        switch (frame.Command)
        {
            case CivCommands.ReadFrequency:
            case CivCommands.SetOutputFreq:
                FrequencyHz = BcdCodec.DecodeFrequency(frame.Data);
                FrequencyChanged?.Invoke(this, FrequencyHz);
                _pendingFreqResponse?.TrySetResult(FrequencyHz);
                break;

            case CivCommands.ReadMode:
            case CivCommands.SetOutputMode:
                if (frame.Data.Length > 0)
                {
                    Mode = ModeCodeToString(frame.Data[0]);
                    ModeChanged?.Invoke(this, Mode);
                    _pendingModeResponse?.TrySetResult(Mode);
                }
                break;

            case CivCommands.ReadMeter when frame.SubCommand == CivCommands.MeterSMeter:
                (SMeterS, SMeterDbm) = MeterDecoder.DecodeSMeter(frame.Data);
                break;

            case CivCommands.ReadMeter when frame.SubCommand == CivCommands.MeterPower:
                RfPowerPercent = MeterDecoder.DecodePercent(frame.Data);
                break;

            case CivCommands.ReadMeter when frame.SubCommand == CivCommands.MeterSwr:
                SwrRatio = MeterDecoder.DecodeSwr(frame.Data);
                break;

            case CivCommands.ReadMeter when frame.SubCommand == CivCommands.MeterAlc:
                AlcLevel = MeterDecoder.DecodePercent(frame.Data);
                break;

            case CivCommands.ReadMeter when frame.SubCommand == CivCommands.MeterVd:
                SupplyVoltage = MeterDecoder.DecodeVoltage(frame.Data);
                break;

            case CivCommands.ReadMeter when frame.SubCommand == CivCommands.MeterId:
                CurrentDraw = MeterDecoder.DecodeCurrent(frame.Data);
                break;

            case CivCommands.PttTunerStatus when frame.SubCommand == CivCommands.PttRx:
                if (frame.Data.Length > 0)
                {
                    PttActive = frame.Data[0] == CivCommands.PttTx;
                    PttChanged?.Invoke(this, PttActive);
                }
                break;

            case CivCommands.ScopeControl when frame.SubCommand == 0x00:
                LastWaveform = ScopeDataDecoder.Decode(frame.Data);
                WaveformUpdated?.Invoke(this, LastWaveform);
                break;
        }
    }

    public async ValueTask DisposeAsync()
    {
        StopPolling();
        StopScope();
        _transport.DataReceived -= OnDataReceived;
        await _transport.DisposeAsync();
    }
}
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

    public event EventHandler<MeterSnapshot>? MeterUpdated;
    public event EventHandler<long>? FrequencyChanged;
    public event EventHandler<string>? ModeChanged;
    public event EventHandler<bool>? PttChanged;

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
    /// Returns null if no response arrives within the timeout.
    public async Task<MemoryChannel?> ReadMemoryChannelAsync(int channelNumber, CancellationToken ct = default)
    {
        long? capturedFreq = null;
        string? capturedMode = null;

        void OnFreq(object? s, long hz) => capturedFreq = hz;
        void OnMode(object? s, string m) => capturedMode = m;

        FrequencyChanged += OnFreq;
        ModeChanged += OnMode;

        try
        {
            await _transport.WriteAsync(_builder.SelectMemoryChannel(channelNumber), ct);
            await Task.Delay(150, ct); // give the radio time to switch

            await _transport.WriteAsync(_builder.ReadFrequency(), ct);
            await Task.Delay(150, ct);

            await _transport.WriteAsync(_builder.ReadMode(), ct);
            await Task.Delay(150, ct);

            if (capturedFreq.HasValue && capturedMode != null)
            {
                return new MemoryChannel(channelNumber, capturedFreq.Value, capturedMode, string.Empty);
            }

            return null;
        }
        finally
        {
            FrequencyChanged -= OnFreq;
            ModeChanged -= OnMode;
        }
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

        System.IO.File.AppendAllText("memory_debug.log",
            $"{DateTime.Now}: LOOP FINISHED, about to return {results.Count} results\n");

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
                // Logging hook will be added when ActivityLogger (Phase 5) exists.
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

    private void OnDataReceived(object? sender, byte[] data)
    {
        var frames = _parser.Feed(data);
        foreach (var frame in frames)
        {
            ApplyFrame(frame);
        }
    }
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
    private void ApplyFrame(CivFrame frame)
    {
        switch (frame.Command)
        {
            case CivCommands.ReadFrequency:
            case CivCommands.SetOutputFreq:
                FrequencyHz = BcdCodec.DecodeFrequency(frame.Data);
                FrequencyChanged?.Invoke(this, FrequencyHz);
                break;
case CivCommands.ReadMode:
case CivCommands.SetOutputMode:
    if (frame.Data.Length > 0)
    {
        Mode = ModeCodeToString(frame.Data[0]);
        ModeChanged?.Invoke(this, Mode);
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
        }
    }

    public async ValueTask DisposeAsync()
    {
        StopPolling();
        _transport.DataReceived -= OnDataReceived;
        await _transport.DisposeAsync();
    }
}
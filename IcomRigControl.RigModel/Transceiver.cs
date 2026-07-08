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

    private void ApplyFrame(CivFrame frame)
    {
        switch (frame.Command)
        {
            case CivCommands.ReadFrequency:
            case CivCommands.SetOutputFreq:
                FrequencyHz = BcdCodec.DecodeFrequency(frame.Data);
                FrequencyChanged?.Invoke(this, FrequencyHz);
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
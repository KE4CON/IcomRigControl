using IcomRigControl.CivEngine;

namespace IcomRigControl.UI.Demo;

/// <summary>
/// A fake ICivTransport that simulates a live radio for UI development and
/// demoing without hardware. Responds to reads with plausible, slightly
/// wandering values so the dashboard looks alive.
/// </summary>
public class DemoCivTransport : ICivTransport
{
    private readonly Random _rng = new();
    private long _frequencyHz = 14_074_000;
    private byte _modeCode = 0x01; // USB

    public bool IsOpen { get; private set; }
    public event EventHandler<byte[]>? DataReceived;

    public Task OpenAsync(CancellationToken ct = default)
    {
        IsOpen = true;
        SimulateReply(BuildModeReply());
        return Task.CompletedTask;
    }
    public Task CloseAsync()
    {
        IsOpen = false;
        return Task.CompletedTask;
    }

    public Task WriteAsync(byte[] data, CancellationToken ct = default)
    {
        // Parse just enough of the outgoing frame to know what was asked for,
        // then synthesize a plausible reply.
        if (data.Length < 5) return Task.CompletedTask;

        byte command = data[4];
        byte? subCommand = data.Length > 5 && data[^1] == CivCommands.EndOfMessage && data.Length > 6
            ? data[5]
            : null;

        switch (command)
        {
            case CivCommands.SetFrequency:
                if (data.Length >= 10)
                {
                    var freqBytes = data[5..10];
                    _frequencyHz = BcdCodec.DecodeFrequency(freqBytes);
                }
                SimulateReply(BuildFreqReply());
                break;

                case CivCommands.SetMode:
                if (data.Length >= 6)
                {
                    _modeCode = data[5];
                }
                SimulateReply(BuildModeReply());
                break;

            case CivCommands.ReadFrequency:
                SimulateReply(BuildFreqReply());
                break;

            case CivCommands.ReadMeter:
                SimulateReply(BuildMeterReply(subCommand ?? 0x00));
                break;
        case CivCommands.ReadMode:
                SimulateReply(BuildModeReply());
                break;
        }
        return Task.CompletedTask;
    }

    private byte[] BuildFreqReply()
    {
        var freqBytes = BcdCodec.EncodeFrequency(_frequencyHz);
        var frame = new List<byte> { 0xFE, 0xFE, 0xE0, 0x94, CivCommands.ReadFrequency };
        frame.AddRange(freqBytes);
        frame.Add(0xFD);
        return frame.ToArray();
    }

    private byte[] BuildModeReply()
    {
        return new byte[] { 0xFE, 0xFE, 0xE0, 0x94, CivCommands.ReadMode, _modeCode, 0x01, 0xFD };
    }

    private byte[] BuildMeterReply(byte subCommand)
    {
        // Generate a slightly wandering raw value 0-255 depending on which meter,
        // so the dashboard shows believable movement.
        int raw = subCommand switch
        {
            CivCommands.MeterSMeter => _rng.Next(90, 130),   // hovering near S9
            CivCommands.MeterPower  => _rng.Next(200, 230),  // ~80-90% power
            CivCommands.MeterSwr    => _rng.Next(0, 20),     // low SWR, healthy
            CivCommands.MeterAlc    => _rng.Next(20, 60),
            CivCommands.MeterVd     => _rng.Next(200, 215),  // ~12.5-13.5V
            CivCommands.MeterId     => _rng.Next(40, 90),
            _ => 0
        };

        byte hi = (byte)(raw / 100);
        byte lo = (byte)(raw % 100);
        byte b0 = (byte)(((hi / 10) << 4) | (hi % 10));
        byte b1 = (byte)(((lo / 10) << 4) | (lo % 10));

        return new byte[] { 0xFE, 0xFE, 0xE0, 0x94, CivCommands.ReadMeter, subCommand, b0, b1, 0xFD };
    }

    private void SimulateReply(byte[] frame)
    {
        // Fire asynchronously-ish so it behaves like real serial data arriving
        // slightly after the request, not synchronously in the same call stack.
        _ = Task.Run(async () =>
        {
            await Task.Delay(20);
            DataReceived?.Invoke(this, frame);
        });
    }

    public ValueTask DisposeAsync()
    {
        IsOpen = false;
        return ValueTask.CompletedTask;
    }
}
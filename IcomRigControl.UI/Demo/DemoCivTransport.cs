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
    private int _selectedMemoryChannel = 0;

    private static readonly Dictionary<int, (long freq, byte mode)> _demoMemories = new()
    {
        { 1,  (14_074_000, 0x01) }, // FT8 20m, USB
        { 2,  (7_074_000,  0x01) }, // FT8 40m, USB
        { 3,  (146_520_000, 0x05) }, // 2m FM calling
        { 4,  (28_074_000, 0x01) }, // FT8 10m, USB
        { 5,  (3_573_000,  0x03) }, // CW 80m
        { 10, (50_313_000, 0x01) }, // 6m calling, USB
    };

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

            case CivCommands.SelectMemory:
                if (data.Length >= 7)
                {
                    byte bcd = data[6];
                    _selectedMemoryChannel = ((bcd >> 4) * 10) + (bcd & 0x0F);
                }
                else
                {
                    _selectedMemoryChannel = 0;
                }
                break;

            case CivCommands.ReadFrequency:
                if (_selectedMemoryChannel > 0 && _demoMemories.TryGetValue(_selectedMemoryChannel, out var mem))
                {
                    _frequencyHz = mem.freq;
                    _modeCode = mem.mode;
                    SimulateReply(BuildFreqReply());
                }
                else if (_selectedMemoryChannel > 0)
                {
                    // Empty channel — no reply, simulating an unprogrammed channel
                }
                else
                {
                    SimulateReply(BuildFreqReply());
                }
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
        int raw = subCommand switch
        {
            CivCommands.MeterSMeter => _rng.Next(90, 130),
            CivCommands.MeterPower  => _rng.Next(200, 230),
            CivCommands.MeterSwr    => _rng.Next(0, 20),
            CivCommands.MeterAlc    => _rng.Next(20, 60),
            CivCommands.MeterVd     => _rng.Next(200, 215),
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
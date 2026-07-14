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
    private bool _scopeOn = false;

    // Fixed demo "signals" that stay at the same position across sweeps,
    // like real transmissions do, so the waterfall shows recognizable
    // vertical streaks instead of random flicker.
    private readonly (int center, int width, int baseHeight)[] _demoSignals =
    {
        (80, 6, 200),
        (220, 4, 160),
        (340, 8, 220),
        (410, 3, 130),
    };

    private static readonly Dictionary<int, (long freq, byte mode)> _demoMemories = new()
    {
        { 1,  (14_074_000, 0x01) },
        { 2,  (7_074_000,  0x01) },
        { 3,  (146_520_000, 0x05) },
        { 4,  (28_074_000, 0x01) },
        { 5,  (3_573_000,  0x03) },
        { 10, (50_313_000, 0x01) },
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
                    // Empty channel — no reply
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

            case CivCommands.ScopeControl when subCommand == 0x10:
                if (data.Length >= 7)
                {
                    _scopeOn = data[6] == 0x01;
                }
                break;

            case CivCommands.ScopeControl when subCommand == 0x11:
                break;

            case CivCommands.ScopeControl when subCommand == 0x00:
                SimulateReply(BuildWaveformReply());
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

    private byte[] BuildWaveformReply()
    {
        // Low, gently-varying noise floor (small variation frame to frame,
        // not a full re-roll) plus a few FIXED-position signals that persist
        // across sweeps, so they render as real-looking vertical streaks.
        var data = new byte[475];
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = (byte)_rng.Next(8, 25);
        }

        foreach (var (center, width, baseHeight) in _demoSignals)
        {
            // Small random jitter in strength so it's not perfectly static,
            // but the position never moves — that's what makes it look real.
            int height = Math.Clamp(baseHeight + _rng.Next(-20, 20), 0, 255);

            for (int x = Math.Max(0, center - width); x < Math.Min(data.Length, center + width); x++)
            {
                int dist = Math.Abs(x - center);
                int falloff = Math.Max(0, height - dist * (height / Math.Max(1, width)));
                data[x] = (byte)Math.Max(data[x], falloff);
            }
        }

        var frame = new List<byte> { 0xFE, 0xFE, 0xE0, 0x94, CivCommands.ScopeControl, 0x00 };
        frame.AddRange(data);
        frame.Add(0xFD);
        return frame.ToArray();
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
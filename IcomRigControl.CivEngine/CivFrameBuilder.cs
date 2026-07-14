namespace IcomRigControl.CivEngine;

public class CivFrameBuilder
{
    private readonly byte _radioAddress;
    private readonly byte _controllerAddress;

    public CivFrameBuilder(byte radioAddress, byte controllerAddress = CivCommands.AddrController)
    {
        _radioAddress = radioAddress;
        _controllerAddress = controllerAddress;
    }

    private byte[] Build(byte command, byte? subCommand = null, byte[]? data = null)
    {
        var frame = new List<byte>
        {
            CivCommands.Preamble,
            CivCommands.Preamble,
            _radioAddress,
            _controllerAddress,
            command
        };
        if (subCommand.HasValue) frame.Add(subCommand.Value);
        if (data != null)        frame.AddRange(data);
        frame.Add(CivCommands.EndOfMessage);
        return frame.ToArray();
    }

    public byte[] ReadFrequency() =>
        Build(CivCommands.ReadFrequency);

    public byte[] SetFrequency(long frequencyHz) =>
        Build(CivCommands.SetFrequency, data: BcdCodec.EncodeFrequency(frequencyHz));

    public byte[] ReadMode() =>
        Build(CivCommands.ReadMode);

    public byte[] SetMode(byte modeCode, byte filter = 0x01) =>
        Build(CivCommands.SetMode, data: new[] { modeCode, filter });

    public byte[] SelectVfoA() =>
        Build(CivCommands.SelectVfo, CivCommands.VfoA);

    public byte[] SelectVfoB() =>
        Build(CivCommands.SelectVfo, CivCommands.VfoB);

    public byte[] SwapVfo() =>
        Build(CivCommands.SelectVfo, CivCommands.VfoSwap);

    public byte[] CopyVfoAToB() =>
        Build(CivCommands.SelectVfo, CivCommands.VfoAEqualsB);

    public byte[] SetPtt(bool transmit) =>
        Build(CivCommands.PttTunerStatus, CivCommands.PttRx,
              new[] { transmit ? CivCommands.PttTx : CivCommands.PttRx });

    public byte[] ReadPtt() =>
        Build(CivCommands.PttTunerStatus, CivCommands.PttRx);

    public byte[] ReadSMeter()  => Build(CivCommands.ReadMeter, CivCommands.MeterSMeter);
    public byte[] ReadPower()   => Build(CivCommands.ReadMeter, CivCommands.MeterPower);
    public byte[] ReadSwr()     => Build(CivCommands.ReadMeter, CivCommands.MeterSwr);
    public byte[] ReadAlc()     => Build(CivCommands.ReadMeter, CivCommands.MeterAlc);
    public byte[] ReadVoltage() => Build(CivCommands.ReadMeter, CivCommands.MeterVd);
    public byte[] ReadCurrent() => Build(CivCommands.ReadMeter, CivCommands.MeterId);

    public byte[] PowerOff() =>
        Build(CivCommands.PowerControl, 0x00);

    // ── Memory Channels ─────────────────────────────────────────────────────

    /// Select a memory channel (1-99) as the active memory (command 08h 00h + channel BCD).
    public byte[] SelectMemoryChannel(int channelNumber)
    {
        var channelBcd = EncodeChannelNumber(channelNumber);
        return Build(CivCommands.SelectMemory, 0x00, channelBcd);
    }

    /// Switch the radio into Memory mode (no channel selection yet).
    public byte[] SwitchToMemoryMode() =>
        Build(CivCommands.SelectMemory);

    /// Read the currently selected memory channel's full content (command 1Ah 00h).
    public byte[] ReadMemoryContent() =>
        Build(CivCommands.MemorySetMenu, 0x00);

    private static byte[] EncodeChannelNumber(int channelNumber)
    {
        // Channel numbers 1-99 encoded as 2-digit BCD (e.g. 5 -> 0x05, 42 -> 0x42)
        int hi = channelNumber / 10;
        int lo = channelNumber % 10;
        return new byte[] { (byte)((hi << 4) | lo) };
    }

    // ── Spectrum Scope ──────────────────────────────────────────────────────

    /// Turn the spectrum scope on or off (command 27h 10h).
    public byte[] SetScopeOn(bool on) =>
        Build(CivCommands.ScopeControl, 0x10, new byte[] { on ? (byte)0x01 : (byte)0x00 });

    /// Enable or disable waveform data output to the PC (command 27h 11h).
    public byte[] SetWaveformOutput(bool enabled) =>
        Build(CivCommands.ScopeControl, 0x11, new byte[] { enabled ? (byte)0x01 : (byte)0x00 });

    /// Set the scope span in Hz (command 27h 15h). Icom expresses span as a
    /// 3-byte BCD code selecting one of several fixed span presets, not a raw
    /// frequency; this maps common span values to their known codes.
    public byte[] SetScopeSpan(long spanHz)
    {
        byte code = spanHz switch
        {
            <= 2_500 => 0x00,
            <= 5_000 => 0x01,
            <= 10_000 => 0x02,
            <= 20_000 => 0x03,
            <= 50_000 => 0x04,
            <= 100_000 => 0x05,
            <= 200_000 => 0x06,
            <= 500_000 => 0x07,
            _ => 0x08 // 1MHz
        };
        return Build(CivCommands.ScopeControl, 0x15, new byte[] { code });
    }

    /// Set scope mode: 0=Center, 1=Fixed (command 27h 14h).
    public byte[] SetScopeMode(bool fixedMode) =>
        Build(CivCommands.ScopeControl, 0x14, new byte[] { fixedMode ? (byte)0x01 : (byte)0x00 });

    /// Request the current waveform data sweep (command 27h 00h).
    /// The radio replies with a multi-byte payload containing up to 475
    /// signal-level data points for the current span.
    public byte[] ReadWaveformData() =>
        Build(CivCommands.ScopeControl, 0x00);
}
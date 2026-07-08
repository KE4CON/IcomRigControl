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
}

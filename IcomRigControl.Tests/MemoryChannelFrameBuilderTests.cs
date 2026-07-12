using IcomRigControl.CivEngine;
using Xunit;

namespace IcomRigControl.Tests;

public class MemoryChannelFrameBuilderTests
{
    [Fact]
    public void SelectMemoryChannel_Channel5_EncodesAsSingleDigitBcd()
    {
        var builder = new CivFrameBuilder(CivCommands.Addr7300);
        var frame = builder.SelectMemoryChannel(5);

        // FE FE 94 E0 08 00 05 FD
        Assert.Equal(0xFE, frame[0]);
        Assert.Equal(CivCommands.SelectMemory, frame[4]);
        Assert.Equal(0x00, frame[5]);
        Assert.Equal(0x05, frame[6]);
        Assert.Equal(0xFD, frame[^1]);
    }

    [Fact]
    public void SelectMemoryChannel_Channel42_EncodesAsTwoDigitBcd()
    {
        var builder = new CivFrameBuilder(CivCommands.Addr7300);
        var frame = builder.SelectMemoryChannel(42);

        Assert.Equal(0x42, frame[6]);
    }

    [Fact]
    public void SelectMemoryChannel_Channel99_EncodesCorrectly()
    {
        var builder = new CivFrameBuilder(CivCommands.Addr7300);
        var frame = builder.SelectMemoryChannel(99);

        Assert.Equal(0x99, frame[6]);
    }

    [Fact]
    public void SwitchToMemoryMode_SendsCorrectCommand()
    {
        var builder = new CivFrameBuilder(CivCommands.Addr7300);
        var frame = builder.SwitchToMemoryMode();

        // FE FE 94 E0 08 FD  (no subcommand, no data)
        Assert.Equal(CivCommands.SelectMemory, frame[4]);
        Assert.Equal(0xFD, frame[^1]);
        Assert.Equal(6, frame.Length);
    }

    [Fact]
    public void ReadMemoryContent_SendsCorrectCommand()
    {
        var builder = new CivFrameBuilder(CivCommands.Addr7300);
        var frame = builder.ReadMemoryContent();

        Assert.Equal(CivCommands.MemorySetMenu, frame[4]);
        Assert.Equal(0x00, frame[5]);
        Assert.Equal(0xFD, frame[^1]);
    }
}
using IcomRigControl.CivEngine;
using Xunit;

namespace IcomRigControl.Tests;

public class ScopeFrameBuilderTests
{
    [Fact]
    public void ScopeOn_SendsCorrectCommand()
    {
        var builder = new CivFrameBuilder(CivCommands.Addr7300);
        var frame = builder.SetScopeOn(true);

        Assert.Equal(CivCommands.ScopeControl, frame[4]);
        Assert.Equal(0x10, frame[5]);
        Assert.Equal(0x01, frame[6]);
        Assert.Equal(0xFD, frame[^1]);
    }

    [Fact]
    public void ScopeOff_SendsCorrectCommand()
    {
        var builder = new CivFrameBuilder(CivCommands.Addr7300);
        var frame = builder.SetScopeOn(false);

        Assert.Equal(0x00, frame[6]);
    }

    [Fact]
    public void SetWaveformOutput_SendsCorrectCommand()
    {
        var builder = new CivFrameBuilder(CivCommands.Addr7300);
        var frame = builder.SetWaveformOutput(true);

        Assert.Equal(CivCommands.ScopeControl, frame[4]);
        Assert.Equal(0x11, frame[5]);
        Assert.Equal(0x01, frame[6]);
    }

    [Fact]
    public void SetScopeSpan_EncodesFrequencyCorrectly()
    {
        var builder = new CivFrameBuilder(CivCommands.Addr7300);
        // 50 kHz span
        var frame = builder.SetScopeSpan(50_000);

        Assert.Equal(CivCommands.ScopeControl, frame[4]);
        Assert.Equal(0x15, frame[5]);
        Assert.Equal(0xFD, frame[^1]);
    }

    [Fact]
    public void ReadWaveformData_SendsCorrectCommand()
    {
        var builder = new CivFrameBuilder(CivCommands.Addr7300);
        var frame = builder.ReadWaveformData();

        Assert.Equal(CivCommands.ScopeControl, frame[4]);
        Assert.Equal(0x00, frame[5]);
        Assert.Equal(0xFD, frame[^1]);
    }
}
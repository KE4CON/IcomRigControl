using System;
using System.Linq;
using IcomRigControl.CivEngine;
using Xunit;

namespace IcomRigControl.Tests;

public class CwKeyerFrameBuilderTests
{
    private static CivFrameBuilder Builder() => new(CivCommands.Addr7300);

    [Fact]
    public void SendCwMessage_EncodesTextAsAsciiInsideA17Frame()
    {
        var frame = Builder().SendCwMessage("CQ");

        Assert.NotNull(frame);
        // FE FE 94 E0 17 'C' 'Q' FD
        Assert.Equal(new byte[] { 0xFE, 0xFE, 0x94, 0xE0, 0x17, (byte)'C', (byte)'Q', 0xFD }, frame);
    }

    [Fact]
    public void SendCwMessage_CapsAt30Characters()
    {
        var frame = Builder().SendCwMessage(new string('A', 50));

        Assert.NotNull(frame);
        // 30 payload chars + 5 framing bytes (FE FE 94 E0 17) + 1 (FD) = 36
        Assert.Equal(30, frame!.Length - 6);
    }

    [Fact]
    public void SendCwMessage_FiltersOutUnsupportedCharacters()
    {
        // Tab and tilde are not in the radio's CW character set; they must be dropped.
        var frame = Builder().SendCwMessage("A\t~B");

        Assert.NotNull(frame);
        Assert.Equal(new byte[] { 0xFE, 0xFE, 0x94, 0xE0, 0x17, (byte)'A', (byte)'B', 0xFD }, frame);
    }

    [Fact]
    public void SendCwMessage_NothingSendable_ReturnsNull()
    {
        Assert.Null(Builder().SendCwMessage("\t\n~"));
        Assert.Null(Builder().SendCwMessage(""));
    }

    [Fact]
    public void AbortCw_SendsCommand17WithFf()
    {
        var frame = Builder().AbortCw();
        Assert.Equal(new byte[] { 0xFE, 0xFE, 0x94, 0xE0, 0x17, 0xFF, 0xFD }, frame);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    public void SendVoiceMemory_BuildsCommand28ForTheSlot(int slot)
    {
        var frame = Builder().SendVoiceMemory(slot);
        // FE FE 94 E0 28 00 <slot> FD
        Assert.Equal(new byte[] { 0xFE, 0xFE, 0x94, 0xE0, 0x28, 0x00, (byte)slot, 0xFD }, frame);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    public void SendVoiceMemory_RejectsOutOfRangeSlot(int slot)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Builder().SendVoiceMemory(slot));
    }

    [Fact]
    public void StopVoiceMemory_BuildsCommand28WithZero()
    {
        var frame = Builder().StopVoiceMemory();
        Assert.Equal(new byte[] { 0xFE, 0xFE, 0x94, 0xE0, 0x28, 0x00, 0x00, 0xFD }, frame);
    }
}

using IcomRigControl.CivEngine;
using Xunit;

namespace IcomRigControl.Tests;

public class CivFrameParserTests
{
    [Fact]
    public void ParsesSingleCompleteFrame()
    {
        var parser = new CivFrameParser();
        // FE FE E0 94 03 00 40 07 14 00 FD  (frequency response, 14.074.000)
        var bytes = new byte[] { 0xFE, 0xFE, 0xE0, 0x94, 0x03, 0x00, 0x40, 0x07, 0x14, 0x00, 0xFD };

        var frames = parser.Feed(bytes);

        Assert.Single(frames);
        Assert.Equal(0xE0, frames[0].To);
        Assert.Equal(0x94, frames[0].From);
        Assert.Equal(0x03, frames[0].Command);
        Assert.Null(frames[0].SubCommand);
        Assert.Equal(new byte[] { 0x00, 0x40, 0x07, 0x14, 0x00 }, frames[0].Data);
    }

    [Fact]
    public void ParsesFrameWithSubCommand()
    {
        var parser = new CivFrameParser();
        // FE FE E0 94 15 02 01 20 FD  (S-meter response, S9)
        var bytes = new byte[] { 0xFE, 0xFE, 0xE0, 0x94, 0x15, 0x02, 0x01, 0x20, 0xFD };

        var frames = parser.Feed(bytes);

        Assert.Single(frames);
        Assert.Equal(0x15, frames[0].Command);
        Assert.Equal((byte)0x02, frames[0].SubCommand);
        Assert.Equal(new byte[] { 0x01, 0x20 }, frames[0].Data);
    }

    [Fact]
    public void ParsesPassResponse()
    {
        var parser = new CivFrameParser();
        var bytes = new byte[] { 0xFE, 0xFE, 0xE0, 0x94, 0xFB, 0xFD };

        var frames = parser.Feed(bytes);

        Assert.Single(frames);
        Assert.True(frames[0].IsPassResponse);
    }

    [Fact]
    public void ParsesMultipleFramesInOneBuffer()
    {
        var parser = new CivFrameParser();
        var bytes = new byte[]
        {
            0xFE, 0xFE, 0xE0, 0x94, 0xFB, 0xFD,
            0xFE, 0xFE, 0xE0, 0x94, 0x15, 0x02, 0x01, 0x20, 0xFD
        };

        var frames = parser.Feed(bytes);

        Assert.Equal(2, frames.Count);
        Assert.True(frames[0].IsPassResponse);
        Assert.Equal(0x15, frames[1].Command);
    }

    [Fact]
    public void HandlesSplitAcrossMultipleFeeds()
    {
        var parser = new CivFrameParser();
        var part1 = new byte[] { 0xFE, 0xFE, 0xE0, 0x94 };
        var part2 = new byte[] { 0x03, 0x00, 0x40, 0x07, 0x14, 0x00, 0xFD };

        var frames1 = parser.Feed(part1);
        Assert.Empty(frames1);

        var frames2 = parser.Feed(part2);
        Assert.Single(frames2);
        Assert.Equal(0x03, frames2[0].Command);
    }

    [Fact]
    public void IgnoresNoiseBeforePreamble()
    {
        var parser = new CivFrameParser();
        var bytes = new byte[]
        {
            0x00, 0x11, // junk bytes before a valid frame
            0xFE, 0xFE, 0xE0, 0x94, 0xFB, 0xFD
        };

        var frames = parser.Feed(bytes);

        Assert.Single(frames);
        Assert.True(frames[0].IsPassResponse);
    }
}

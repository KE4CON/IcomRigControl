using System.Linq;
using System.Text;
using IcomRigControl.CivEngine;
using Xunit;

namespace IcomRigControl.Tests;

public class Ax25FcsTests
{
    [Fact]
    public void Compute_MatchesTheCrc16X25CheckValue()
    {
        // The standard CRC-16/X-25 check value for the ASCII string "123456789"
        // is 0x906E — this pins our FCS to the real AX.25 algorithm.
        Assert.Equal((ushort)0x906E, Ax25Fcs.Compute(Encoding.ASCII.GetBytes("123456789")));
    }

    [Fact]
    public void ComputeBytes_AreLowByteFirst_AndCheckAccepts()
    {
        var frame = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var fcs = Ax25Fcs.ComputeBytes(frame);
        ushort crc = Ax25Fcs.Compute(frame);

        Assert.Equal((byte)(crc & 0xFF), fcs[0]); // low byte first
        Assert.Equal((byte)(crc >> 8), fcs[1]);

        Assert.True(Ax25Fcs.Check(frame.Concat(fcs).ToArray()));
    }

    [Fact]
    public void Check_RejectsACorruptedFrame()
    {
        var frame = new byte[] { 0x10, 0x20, 0x30 };
        var withFcs = frame.Concat(Ax25Fcs.ComputeBytes(frame)).ToArray();
        withFcs[0] ^= 0xFF; // flip a bit

        Assert.False(Ax25Fcs.Check(withFcs));
    }

    [Fact]
    public void ModulateAx25Frame_AddsPreambleFlagsAndFcs_SoIsLongerThanRawModulate()
    {
        var frame = Ax25FrameBuilder.BuildUiFrame("KE4CON", 9, "APRS", 0, "!4903.50N/07201.75W-Test");

        var raw = AfskModulator.ModulateFrame(frame, AfskProfile.Hf300Baud, 44100);
        var full = AfskModulator.ModulateAx25Frame(frame, AfskProfile.Hf300Baud, 44100);

        Assert.True(full.Length > raw.Length,
            "The on-air frame must add flags + FCS, so it is longer than the bare modulation.");
    }
}

using IcomRigControl.CivEngine;
using Xunit;

namespace IcomRigControl.Tests;

public class BcdCodecTests
{
    [Theory]
    [InlineData(14_074_000,  new byte[] { 0x00, 0x40, 0x07, 0x14, 0x00 })]
    [InlineData(7_200_000,   new byte[] { 0x00, 0x00, 0x20, 0x07, 0x00 })]
    [InlineData(144_200_000, new byte[] { 0x00, 0x00, 0x20, 0x44, 0x01 })]
    [InlineData(1_000_000,   new byte[] { 0x00, 0x00, 0x00, 0x01, 0x00 })]
    [InlineData(0,           new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00 })]
    public void EncodeFrequency_ReturnsCorrectBcd(long hz, byte[] expected)
    {
        var result = BcdCodec.EncodeFrequency(hz);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(new byte[] { 0x00, 0x40, 0x07, 0x14, 0x00 }, 14_074_000)]
    [InlineData(new byte[] { 0x00, 0x00, 0x20, 0x07, 0x00 }, 7_200_000)]
    [InlineData(new byte[] { 0x00, 0x00, 0x20, 0x44, 0x01 }, 144_200_000)]
    [InlineData(new byte[] { 0x00, 0x00, 0x00, 0x01, 0x00 }, 1_000_000)]
    [InlineData(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00 }, 0)]
    public void DecodeFrequency_ReturnsCorrectHz(byte[] bcd, long expectedHz)
    {
        var result = BcdCodec.DecodeFrequency(bcd);
        Assert.Equal(expectedHz, result);
    }

    [Theory]
    [InlineData(14_074_000)]
    [InlineData(7_200_000)]
    [InlineData(50_313_000)]
    [InlineData(144_200_000)]
    public void RoundTrip_EncodeDecodeReturnsOriginal(long hz)
    {
        var encoded = BcdCodec.EncodeFrequency(hz);
        var decoded = BcdCodec.DecodeFrequency(encoded);
        Assert.Equal(hz, decoded);
    }
}

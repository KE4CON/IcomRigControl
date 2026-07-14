using IcomRigControl.CivEngine;
using Xunit;

namespace IcomRigControl.Tests;

public class ScopeDataDecoderTests
{
    [Fact]
    public void Decode_EmptyData_ReturnsEmptyArray()
    {
        var result = ScopeDataDecoder.Decode(System.Array.Empty<byte>());
        Assert.Empty(result);
    }

    [Fact]
    public void Decode_SingleByte_ReturnsSingleValue()
    {
        // Raw scope bytes are 0-255 representing relative signal level
        var result = ScopeDataDecoder.Decode(new byte[] { 0x80 });
        Assert.Single(result);
        Assert.Equal(128, result[0]);
    }

    [Fact]
    public void Decode_MultipleBytes_ReturnsCorrectCount()
    {
        var data = new byte[475];
        for (int i = 0; i < data.Length; i++) data[i] = (byte)(i % 256);

        var result = ScopeDataDecoder.Decode(data);

        Assert.Equal(475, result.Length);
        Assert.Equal(0, result[0]);
        Assert.Equal(255, result[255]);
    }

    [Fact]
    public void NormalizeToPercent_ZeroRaw_IsZeroPercent()
    {
        var result = ScopeDataDecoder.NormalizeToPercent(0);
        Assert.Equal(0.0, result);
    }

    [Fact]
    public void NormalizeToPercent_MaxRaw_Is100Percent()
    {
        var result = ScopeDataDecoder.NormalizeToPercent(255);
        Assert.Equal(100.0, result, precision: 0);
    }

    [Fact]
    public void NormalizeToPercent_MidRaw_IsRoughlyHalf()
    {
        var result = ScopeDataDecoder.NormalizeToPercent(128);
        Assert.InRange(result, 49.0, 51.0);
    }
}
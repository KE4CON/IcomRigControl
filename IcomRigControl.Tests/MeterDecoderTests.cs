using IcomRigControl.CivEngine;
using Xunit;

namespace IcomRigControl.Tests;

public class MeterDecoderTests
{
    [Theory]
    [InlineData(new byte[] { 0x00, 0x00 }, 0)]
    [InlineData(new byte[] { 0x01, 0x20 }, 120)]
    [InlineData(new byte[] { 0x02, 0x41 }, 241)]
    [InlineData(new byte[] { 0x02, 0x55 }, 255)]
    public void DecodeRawLevel_ReturnsCorrectInteger(byte[] data, int expected)
    {
        var result = MeterDecoder.DecodeRawLevel(data);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void DecodeSMeter_ZeroRaw_IsS0()
    {
        var (sUnit, _) = MeterDecoder.DecodeSMeter(new byte[] { 0x00, 0x00 });
        Assert.Equal(0, sUnit);
    }

    [Fact]
    public void DecodeSMeter_120Raw_IsS9()
    {
        var (sUnit, _) = MeterDecoder.DecodeSMeter(new byte[] { 0x01, 0x20 });
        Assert.Equal(9, sUnit);
    }

    [Fact]
    public void DecodeSMeter_241Raw_IsS9Plus60()
    {
        var (sUnit, dBm) = MeterDecoder.DecodeSMeter(new byte[] { 0x02, 0x41 });
        Assert.Equal(9, sUnit);
        Assert.True(dBm > 0);
    }

    [Fact]
    public void DecodePercent_MaxRaw_Is100Percent()
    {
        var result = MeterDecoder.DecodePercent(new byte[] { 0x02, 0x55 });
        Assert.Equal(100.0, result, precision: 0);
    }

    [Fact]
    public void DecodePercent_ZeroRaw_IsZeroPercent()
    {
        var result = MeterDecoder.DecodePercent(new byte[] { 0x00, 0x00 });
        Assert.Equal(0.0, result);
    }

    [Fact]
    public void DecodeSwr_ZeroRaw_Is1to1()
    {
        var result = MeterDecoder.DecodeSwr(new byte[] { 0x00, 0x00 });
        Assert.Equal(1.0, result, precision: 1);
    }

    [Fact]
    public void DecodeSwr_HighRaw_IsHighRatio()
    {
        var result = MeterDecoder.DecodeSwr(new byte[] { 0x02, 0x40 });
        Assert.True(result >= 9.0);
    }

    [Fact]
    public void DecodeVoltage_MaxRaw_Is16Volts()
    {
        var result = MeterDecoder.DecodeVoltage(new byte[] { 0x02, 0x55 });
        Assert.Equal(16.0, result, precision: 0);
    }

    [Fact]
    public void DecodeCurrent_MaxRaw_Is25Amps()
    {
        var result = MeterDecoder.DecodeCurrent(new byte[] { 0x02, 0x55 });
        Assert.Equal(25.0, result, precision: 0);
    }
}

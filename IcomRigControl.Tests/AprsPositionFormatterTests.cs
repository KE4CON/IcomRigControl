using IcomRigControl.CivEngine;
using Xunit;

namespace IcomRigControl.Tests;

public class AprsPositionFormatterTests
{
    [Fact]
    public void FormatPosition_MatchesSpecWorkedExample()
    {
        // From the official APRS 1.0.1 spec worked example:
        // !4903.50N/07201.75W-  (49 deg 3.50 min N, 72 deg 1.75 min W)
        string result = AprsPositionFormatter.FormatPosition(
            latitude: 49.058333, longitude: -72.029167,
            symbolTable: '/', symbolCode: '-', comment: "");

        Assert.Equal("!4903.50N/07201.75W-", result);
    }

    [Fact]
    public void FormatPosition_IncludesCommentAfterSymbolCode()
    {
        string result = AprsPositionFormatter.FormatPosition(
            latitude: 49.058333, longitude: -72.029167,
            symbolTable: '/', symbolCode: '-', comment: "Test1234");

        Assert.Equal("!4903.50N/07201.75W-Test1234", result);
    }

    [Fact]
    public void FormatPosition_SouthernLatitude_UsesS()
    {
        string result = AprsPositionFormatter.FormatPosition(
            latitude: -33.5, longitude: 0, symbolTable: '/', symbolCode: '-', comment: "");

        Assert.Contains("S", result);
        Assert.DoesNotContain("N", result[..9]); // latitude portion specifically
    }

    [Fact]
    public void FormatPosition_EasternLongitude_UsesE()
    {
        string result = AprsPositionFormatter.FormatPosition(
            latitude: 0, longitude: 120.5, symbolTable: '/', symbolCode: '-', comment: "");

        Assert.Contains("E", result);
    }

    [Fact]
    public void FormatPosition_LatitudeIsAlways8CharsWithLeadingZero()
    {
        // 5 deg 2.50 min N should still be 8 chars: "0502.50N"
        string result = AprsPositionFormatter.FormatPosition(
            latitude: 5.041667, longitude: 0, symbolTable: '/', symbolCode: '-', comment: "");

        string latPart = result[1..9];
        Assert.Equal(8, latPart.Length);
        Assert.StartsWith("0502.5", latPart);
    }

    [Fact]
    public void FormatPosition_LongitudeIsAlways9CharsWithLeadingZeros()
    {
        // 5 deg 2.50 min W should be 9 chars: "00502.50W"
        string result = AprsPositionFormatter.FormatPosition(
            latitude: 0, longitude: -5.041667, symbolTable: '/', symbolCode: '-', comment: "");

        // Position after "!DDMM.MMN" (9 chars) + symbol table (1 char) = index 10
        string lonPart = result[10..19];
        Assert.Equal(9, lonPart.Length);
        Assert.StartsWith("00502.5", lonPart);
    }

    [Fact]
    public void FormatPosition_LatitudeOutOfRange_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AprsPositionFormatter.FormatPosition(91, 0, '/', '-', ""));
    }

    [Fact]
    public void FormatPosition_LongitudeOutOfRange_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AprsPositionFormatter.FormatPosition(0, 181, '/', '-', ""));
    }

    [Fact]
    public void FormatFrequencyBeaconComment_IncludesFrequencyInMHz()
    {
        string comment = AprsPositionFormatter.FormatFrequencyBeaconComment(14_074_000, "USB");
        Assert.Contains("14.074", comment);
        Assert.Contains("USB", comment);
    }
}
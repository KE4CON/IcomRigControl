using IcomRigControl.Services;
using Xunit;

namespace IcomRigControl.Tests;

public class DxSpotParserTests
{
    [Fact]
    public void Parse_StandardSpot_ExtractsAllFields()
    {
        var spot = DxSpotParser.Parse("DX de W3LPL:      14074.0  K1ABC        FT8 -12 dB          1305Z");

        Assert.NotNull(spot);
        Assert.Equal("K1ABC", spot!.DxCallsign);
        Assert.Equal(14_074_000, spot.FrequencyHz);
        Assert.Equal("W3LPL", spot.Spotter);
        Assert.Contains("FT8", spot.Comment);
        Assert.Equal("1305Z", spot.TimeUtc);
    }

    [Fact]
    public void Parse_FrequencyIsConvertedFromKhzToHz()
    {
        var spot = DxSpotParser.Parse("DX de EA1ABC: 7005.5 DL1XYZ CW 599 1200Z");
        Assert.NotNull(spot);
        Assert.Equal(7_005_500, spot!.FrequencyHz);
    }

    [Fact]
    public void Parse_RbnStyleSpotterWithSuffix_IsHandled()
    {
        // Reverse Beacon Network spotters look like "RBN/DL0XYZ-#".
        var spot = DxSpotParser.Parse("DX de RBN/DL0XYZ-#: 14025.0 OH2XX CW 25 dB 22 WPM CQ 1307Z");

        Assert.NotNull(spot);
        Assert.Equal("OH2XX", spot!.DxCallsign);
        Assert.Equal(14_025_000, spot.FrequencyHz);
        Assert.Equal("RBN/DL0XYZ-#", spot.Spotter);
    }

    [Fact]
    public void Parse_PortableAndSlashedCallsigns_AreKept()
    {
        var spot = DxSpotParser.Parse("DX de K1ABC: 21200.0 VP8/G4XYZ SSB 59 1400Z");
        Assert.NotNull(spot);
        Assert.Equal("VP8/G4XYZ", spot!.DxCallsign);
    }

    [Fact]
    public void Parse_SpotWithoutTrailingTime_StillParses()
    {
        var spot = DxSpotParser.Parse("DX de K1ABC: 14074.0 JA1XYZ FT8");
        Assert.NotNull(spot);
        Assert.Equal("JA1XYZ", spot!.DxCallsign);
        Assert.Equal(14_074_000, spot.FrequencyHz);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("Hello and welcome to the DX Cluster")]
    [InlineData("WWV de W0MU <14>:   SFI=140, A=5, K=2")]
    [InlineData("login: ")]
    [InlineData("To ALL de K1ABC: see you on 20m")]
    public void Parse_NonSpotLines_ReturnNull(string? line)
    {
        Assert.Null(DxSpotParser.Parse(line));
    }
}

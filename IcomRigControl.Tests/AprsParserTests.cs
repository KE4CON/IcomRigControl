using IcomRigControl.CivEngine;
using Xunit;

namespace IcomRigControl.Tests;

public class AprsParserTests
{
    [Fact]
    public void Parse_UncompressedPosition_ExtractsLatLonSymbolAndComment()
    {
        var p = AprsParser.Parse("!4903.50N/07201.75W-Test HF APRS");

        Assert.Equal(AprsPacketType.Position, p.Type);
        Assert.Equal(49.0583, p.Latitude!.Value, 3);   // 49 + 3.50/60
        Assert.Equal(-72.0292, p.Longitude!.Value, 3); // -(72 + 1.75/60)
        Assert.Equal('/', p.SymbolTable);
        Assert.Equal('-', p.SymbolCode);
        Assert.Equal("Test HF APRS", p.Comment);
    }

    [Fact]
    public void Parse_SouthAndEastHemispheres_AreSigned()
    {
        var p = AprsParser.Parse("!3358.00S/15112.00E>Sydney");
        Assert.True(p.Latitude!.Value < 0);   // S
        Assert.True(p.Longitude!.Value > 0);  // E
    }

    [Fact]
    public void Parse_Message_ExtractsAddresseeAndText_StrippingMsgNo()
    {
        var p = AprsParser.Parse(":KE4CON   :hello there{001");

        Assert.Equal(AprsPacketType.Message, p.Type);
        Assert.Equal("KE4CON", p.MessageAddressee);
        Assert.Equal("hello there", p.MessageText);
    }

    [Fact]
    public void Parse_StatusAndUnparsed()
    {
        Assert.Equal(AprsPacketType.Status, AprsParser.Parse(">On the air on 20m HF").Type);
        Assert.Equal(AprsPacketType.Other, AprsParser.Parse("not an apres packet").Type);
    }

    [Fact]
    public void FormatThenParse_RoundTripsAPosition()
    {
        // Our own beacon's formatter -> our parser must agree.
        string info = AprsPositionFormatter.FormatPosition(49.0583, -72.0292, '/', '-', "hi");
        var p = AprsParser.Parse(info);

        Assert.Equal(AprsPacketType.Position, p.Type);
        Assert.Equal(49.0583, p.Latitude!.Value, 2);
        Assert.Equal(-72.0292, p.Longitude!.Value, 2);
    }
}

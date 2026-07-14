using IcomRigControl.Services;
using Xunit;

namespace IcomRigControl.Tests;

public class ContactPacketParserTests
{
    private const string SampleContactXml = @"<contactinfo>
  <app>N1MM</app>
  <contestname>ARRL-FD</contestname>
  <call>W1AW</call>
  <band>20</band>
  <mode>USB</mode>
  <mycall>KE4CON</mycall>
  <exchange1>3A</exchange1>
  <sect>GA</sect>
  <rxfreq>14074000</rxfreq>
  <txfreq>14074000</txfreq>
  <timestamp>2026-07-14 20:30:00</timestamp>
  <snt>59</snt>
  <rcv>59</rcv>
</contactinfo>";

    [Fact]
    public void Parse_ExtractsCallsign()
    {
        var result = ContactPacketParser.Parse(SampleContactXml);
        Assert.NotNull(result);
        Assert.Equal("W1AW", result!.Callsign);
    }

    [Fact]
    public void Parse_ExtractsFrequencyInMHz()
    {
        var result = ContactPacketParser.Parse(SampleContactXml);
        Assert.NotNull(result);
        Assert.Equal(14.074, result!.FrequencyMHz, precision: 3);
    }

    [Fact]
    public void Parse_ExtractsMode()
    {
        var result = ContactPacketParser.Parse(SampleContactXml);
        Assert.NotNull(result);
        Assert.Equal("USB", result!.Mode);
    }

    [Fact]
    public void Parse_ExtractsRstSentAndReceived()
    {
        var result = ContactPacketParser.Parse(SampleContactXml);
        Assert.NotNull(result);
        Assert.Equal("59", result!.RstSent);
        Assert.Equal("59", result.RstReceived);
    }

    [Fact]
    public void Parse_ExtractsContestExchangeFields()
    {
        var result = ContactPacketParser.Parse(SampleContactXml);
        Assert.NotNull(result);
        Assert.Equal("3A", result!.ContestExchangeSent);
        Assert.Equal("GA", result.ContestExchangeReceived);
    }

    [Fact]
    public void Parse_ExtractsTimestamp()
    {
        var result = ContactPacketParser.Parse(SampleContactXml);
        Assert.NotNull(result);
        Assert.Equal(new DateTime(2026, 7, 14, 20, 30, 0, DateTimeKind.Utc), result!.DateTimeUtc);
    }

    [Fact]
    public void Parse_MalformedXml_ReturnsNull()
    {
        var result = ContactPacketParser.Parse("not valid xml at all <<<");
        Assert.Null(result);
    }

    [Fact]
    public void Parse_NonContactXml_ReturnsNull()
    {
        var result = ContactPacketParser.Parse("<RadioInfo><StationName>Test</StationName></RadioInfo>");
        Assert.Null(result);
    }

    [Fact]
    public void Parse_MissingCallsign_ReturnsNull()
    {
        var xml = @"<contactinfo><band>20</band><mode>USB</mode></contactinfo>";
        var result = ContactPacketParser.Parse(xml);
        Assert.Null(result);
    }
}
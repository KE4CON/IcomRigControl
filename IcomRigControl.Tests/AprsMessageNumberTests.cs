using IcomRigControl.CivEngine;
using Xunit;

namespace IcomRigControl.Tests;

public class AprsMessageNumberTests
{
    [Fact]
    public void ParsesMessage_WithLineNumber()
    {
        // ":KE4CON   :hello there{042"  — addressee padded to 9, text, then {number.
        var pkt = AprsParser.Parse(":KE4CON   :hello there{042");
        Assert.Equal(AprsPacketType.Message, pkt.Type);
        Assert.Equal("KE4CON", pkt.MessageAddressee);
        Assert.Equal("hello there", pkt.MessageText);
        Assert.Equal("042", pkt.MessageNumber);
    }

    [Fact]
    public void PlainMessage_HasNoNumber()
    {
        var pkt = AprsParser.Parse(":KE4CON   :no number here");
        Assert.Equal("no number here", pkt.MessageText);
        Assert.Null(pkt.MessageNumber);
    }

    [Fact]
    public void Ack_HasNoNumber_SoItWontBeAckedBack()
    {
        // An ACK itself carries no "{nn", so MessageNumber stays null — the guard
        // that only auto-ACKs numbered messages will skip it (no ACK loops).
        var pkt = AprsParser.Parse(":KE4CON   :ack042");
        Assert.Equal("ack042", pkt.MessageText);
        Assert.Null(pkt.MessageNumber);
    }
}

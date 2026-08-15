using IcomRigControl.CivEngine;
using Xunit;

namespace IcomRigControl.Tests;

public class CallsignValidatorTests
{
    [Theory]
    [InlineData("KE4CON")]
    [InlineData("W1AW")]
    [InlineData("K1A")]
    [InlineData("VP8ABC")]
    [InlineData("2E0ABC")]
    public void AcceptsRealCallsigns(string callsign)
    {
        Assert.True(CallsignValidator.IsPlausibleAmateurCallsign(callsign));
    }

    [Theory]
    [InlineData(null)]           // never configured
    [InlineData("")]             // blank
    [InlineData("   ")]          // whitespace
    [InlineData("NOCALL")]       // classic placeholder
    [InlineData("N0CALL")]       // classic placeholder
    [InlineData("TEST")]         // placeholder / no digit anyway
    [InlineData("MYCALL")]       // placeholder
    [InlineData("HELLO")]        // no digit — not a callsign
    [InlineData("12345")]        // no letter — not a callsign
    [InlineData("W1AW-9")]       // SSID/punctuation not allowed in the base call
    [InlineData("A1")]           // too short
    [InlineData("KE4CON1")]      // too long for the AX.25 address field
    public void RejectsBlankPlaceholderOrMalformed(string? callsign)
    {
        Assert.False(CallsignValidator.IsPlausibleAmateurCallsign(callsign));
    }
}

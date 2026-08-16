using System;
using IcomRigControl.Services;
using Xunit;

namespace IcomRigControl.Tests;

public class NtpClientTests
{
    [Fact]
    public void ParseTransmitTimestamp_DecodesSecondsSince1900()
    {
        // Build a response whose transmit timestamp is exactly N seconds past the NTP
        // epoch (1900-01-01), with zero fraction, and confirm it round-trips.
        uint seconds = 3_800_000_000; // ~2020-ish, fits in 32 bits
        var data = new byte[48];
        data[40] = (byte)(seconds >> 24);
        data[41] = (byte)(seconds >> 16);
        data[42] = (byte)(seconds >> 8);
        data[43] = (byte)seconds;

        DateTime expected = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(seconds);
        DateTime actual = NtpClient.ParseTransmitTimestamp(data);

        Assert.Equal(DateTimeKind.Utc, actual.Kind);
        Assert.True(Math.Abs((expected - actual).TotalMilliseconds) < 1);
    }
}

using System;
using System.Collections.Generic;
using IcomRigControl.Services;
using Xunit;

namespace IcomRigControl.Tests;

public class SpotNeedAnalyzerTests
{
    private static QsoRecord Qso(string call, string band) =>
        new(call, 14.074, band, "USB", DateTime.UtcNow, "59", "59");

    [Fact]
    public void FlagsNewCall_AndNewOnBand()
    {
        var log = new List<QsoRecord> { Qso("KE4CON", "20M"), Qso("W1AW", "40M") };
        var a = new SpotNeedAnalyzer(log);

        // Worked KE4CON on 20m already.
        Assert.False(a.IsNewCall("KE4CON"));
        Assert.False(a.IsNewOnBand("KE4CON", 14_074_000));   // 20m — worked
        Assert.True(a.IsNewOnBand("KE4CON", 7_074_000));      // 40m — not worked (new band)

        // Never worked DL1ABC at all.
        Assert.True(a.IsNewCall("DL1ABC"));
        Assert.True(a.IsNewOnBand("DL1ABC", 14_074_000));
    }

    [Fact]
    public void IsCaseInsensitive()
    {
        var a = new SpotNeedAnalyzer(new[] { Qso("ke4con", "20M") });
        Assert.False(a.IsNewOnBand("KE4CON", 14_074_000));
    }
}

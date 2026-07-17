using IcomRigControl.Services;
using Xunit;

namespace IcomRigControl.Tests;

public class RttyRoundupTests
{
    [Fact]
    public void RttyRoundup_HasCorrectName()
    {
        Assert.Equal("ARRL RTTY Roundup", ContestCatalog.RttyRoundup.Name);
    }

    [Fact]
    public void RttyRoundup_HasRstAndExchangeFields()
    {
        Assert.Equal(2, ContestCatalog.RttyRoundup.ExchangeFieldLabels.Count);
        Assert.Contains("RST", ContestCatalog.RttyRoundup.ExchangeFieldLabels);
    }

    [Fact]
    public void RttyRoundup_RestrictsToFiveHfBands()
    {
        Assert.Equal(5, ContestCatalog.RttyRoundup.RestrictedBands.Count);
        Assert.Contains("20M", ContestCatalog.RttyRoundup.RestrictedBands);
        Assert.DoesNotContain("6M", ContestCatalog.RttyRoundup.RestrictedBands);
    }

    [Fact]
    public void RttyRoundup_EveryQsoIsWorthOnePoint()
    {
        Assert.Equal(1, ContestCatalog.RttyRoundup.PointsForMode("RTTY"));
    }

    [Fact]
    public void RttyRoundup_SameStationSameBand_IsDuplicate()
    {
        var existing = new List<QsoRecord>
        {
            new QsoRecord("W1AW", 14.080, "20M", "RTTY", DateTime.UtcNow, "599", "599")
        };

        bool isDupe = ContestCatalog.RttyRoundup.IsDuplicate(existing, "W1AW", "20M", "RTTY");

        Assert.True(isDupe);
    }

    [Fact]
    public void RttyRoundup_SameStationDifferentBand_IsNotDuplicate()
    {
        var existing = new List<QsoRecord>
        {
            new QsoRecord("W1AW", 14.080, "20M", "RTTY", DateTime.UtcNow, "599", "599")
        };

        bool isDupe = ContestCatalog.RttyRoundup.IsDuplicate(existing, "W1AW", "40M", "RTTY");

        Assert.False(isDupe);
    }
}
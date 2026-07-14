using IcomRigControl.Services;
using Xunit;

namespace IcomRigControl.Tests;

public class ContestDefinitionTests
{
    [Fact]
    public void FieldDay_HasCorrectName()
    {
        var fd = ContestCatalog.FieldDay;
        Assert.Equal("ARRL Field Day", fd.Name);
    }

    [Fact]
    public void FieldDay_ExchangeFieldsAreClassAndSection()
    {
        var fd = ContestCatalog.FieldDay;
        Assert.Equal(2, fd.ExchangeFieldLabels.Count);
        Assert.Contains("Class", fd.ExchangeFieldLabels);
        Assert.Contains("Section", fd.ExchangeFieldLabels);
    }

    [Fact]
    public void FieldDay_AllowsAllBands()
    {
        var fd = ContestCatalog.FieldDay;
        Assert.Empty(fd.RestrictedBands); // empty = no restriction, all bands allowed
    }

    [Theory]
    [InlineData("CW", 2)]
    [InlineData("USB", 1)]
    [InlineData("LSB", 1)]
    [InlineData("FT8", 2)]
    public void FieldDay_PointsPerQsoVaryByMode(string mode, int expectedPoints)
    {
        var fd = ContestCatalog.FieldDay;
        Assert.Equal(expectedPoints, fd.PointsForMode(mode));
    }

    [Fact]
    public void FieldDay_DupeCheck_SameCallSameBandSameMode_IsDupe()
    {
        var fd = ContestCatalog.FieldDay;
        var existing = new[]
        {
            new QsoRecord("W1AW", 14.074, "20M", "USB", DateTime.UtcNow, "59", "59",
                ContestExchangeSent: "3A GA", ContestExchangeReceived: "5A OH")
        };

        bool isDupe = fd.IsDuplicate(existing, "W1AW", "20M", "USB");
        Assert.True(isDupe);
    }

    [Fact]
    public void FieldDay_DupeCheck_SameCallDifferentBand_IsNotDupe()
    {
        var fd = ContestCatalog.FieldDay;
        var existing = new[]
        {
            new QsoRecord("W1AW", 14.074, "20M", "USB", DateTime.UtcNow, "59", "59")
        };

        bool isDupe = fd.IsDuplicate(existing, "W1AW", "40M", "USB");
        Assert.False(isDupe);
    }

    [Fact]
    public void FieldDay_DupeCheck_DifferentCall_IsNotDupe()
    {
        var fd = ContestCatalog.FieldDay;
        var existing = new[]
        {
            new QsoRecord("W1AW", 14.074, "20M", "USB", DateTime.UtcNow, "59", "59")
        };

        bool isDupe = fd.IsDuplicate(existing, "K1ABC", "20M", "USB");
        Assert.False(isDupe);
    }
}
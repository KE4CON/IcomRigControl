using IcomRigControl.Services;
using Xunit;

namespace IcomRigControl.Tests;

public class DxccResolverTests
{
    [Theory]
    [InlineData("W1AW", "United States")]
    [InlineData("K5XYZ", "United States")]
    [InlineData("KH6ABC", "Hawaii")]        // longer prefix beats generic "K"
    [InlineData("KL7AA", "Alaska")]
    [InlineData("KP4XY", "Puerto Rico")]
    [InlineData("G3ABC", "England")]
    [InlineData("GM4XYZ", "Scotland")]      // GM beats G
    [InlineData("DL1ABC", "Germany")]
    [InlineData("JA1XYZ", "Japan")]
    [InlineData("VK2DEF", "Australia")]
    [InlineData("ZL1AB", "New Zealand")]
    public void Resolve_KnownCalls(string call, string entity) =>
        Assert.Equal(entity, DxccResolver.Resolve(call));

    [Fact]
    public void Resolve_UnknownPrefix_IsUnknown() =>
        Assert.Equal("Unknown", DxccResolver.Resolve("QZ9ZZ"));
}

public class AwardTrackerTests
{
    [Fact]
    public void CountsDistinctEntitiesAndGrids_AndFindsNewOnes()
    {
        var worked = new[]
        {
            new WorkedContact("W1AW", "20M", "FN31"),
            new WorkedContact("K5XYZ", "40M", "EM12"),   // also United States
            new WorkedContact("DL1ABC", "20M", "JO31"),
            new WorkedContact("JA1XYZ", "15M", null),
        };
        var t = new AwardTracker(worked);

        Assert.Equal(3, t.EntityCount);   // US, Germany, Japan (US counted once)
        Assert.Equal(3, t.GridCount);     // FN31, EM12, JO31

        Assert.False(t.IsNewEntity("N6AA"));   // United States — worked
        Assert.True(t.IsNewEntity("VK2DEF"));  // Australia — not worked -> new one
        Assert.False(t.IsNewEntity("QZ9ZZ"));  // Unknown entity is never flagged "new"
    }
}

using System;
using IcomRigControl.Services;
using Xunit;

namespace IcomRigControl.Tests;

public class GreyLineTests
{
    [Fact]
    public void Equator_AtEquinox_RisesAroundSixAndSetsAroundEighteen()
    {
        // Lat/lon 0 on the equinox: sunrise ~06:00 UTC, sunset ~18:00 UTC.
        var date = new DateTime(2026, 3, 20, 0, 0, 0, DateTimeKind.Utc);
        var r = GreyLine.SunriseSunset(0, 0, date);

        Assert.NotNull(r);
        var (rise, set) = r!.Value;
        Assert.True(rise < set, "sunrise must be before sunset");
        Assert.InRange(rise.Hour, 5, 6);
        Assert.InRange(set.Hour, 17, 18);

        // Solar noon (midpoint) is near 12:00 UTC at longitude 0.
        double noonHour = (rise.TimeOfDay + (set - rise) / 2).TotalHours;
        Assert.InRange(noonHour, 11.5, 12.5);
    }

    [Theory]
    // Solar noon (the sunrise/sunset midpoint) shifts by lon/15 hours: west (negative
    // east-longitude) is later in UTC, east is earlier. Exercises the longitude term of
    // the sunrise equation, which the day=0 tests (lon 0) never touch.
    [InlineData(-75.0, 17.0)]  // ~US East Coast: noon ~12:00 + 5h = 17:00 UTC
    [InlineData(75.0, 7.0)]    // ~Central Asia: noon ~12:00 - 5h = 07:00 UTC
    public void SolarNoon_ShiftsWithLongitude(double lonDeg, double expectedNoonHourUtc)
    {
        var date = new DateTime(2026, 3, 20, 0, 0, 0, DateTimeKind.Utc);
        var r = GreyLine.SunriseSunset(0, lonDeg, date);
        Assert.NotNull(r);
        var (rise, set) = r!.Value;
        double noonHour = (rise.TimeOfDay + (set - rise) / 2).TotalHours;
        Assert.InRange(noonHour, expectedNoonHourUtc - 0.5, expectedNoonHourUtc + 0.5);
    }

    [Fact]
    public void HighArctic_InSummer_HasNoSunset()
    {
        // Above the Arctic circle at summer solstice: midnight sun -> null.
        var solstice = new DateTime(2026, 6, 21, 0, 0, 0, DateTimeKind.Utc);
        Assert.Null(GreyLine.SunriseSunset(80, 0, solstice));
    }

    [Fact]
    public void StatusAt_IsActive_InsideASunriseWindow()
    {
        // Equator/equinox sunrise ~06:00 UTC; a moment right at it is inside the window.
        var atSunrise = new DateTime(2026, 3, 20, 6, 0, 0, DateTimeKind.Utc);
        var st = GreyLine.StatusAt(0, 0, atSunrise);
        Assert.True(st.IsActive);
        Assert.NotNull(st.Current);
        Assert.NotNull(st.UntilEnd);
    }

    [Fact]
    public void StatusAt_ReportsNextWindow_WhenNotActive()
    {
        // Local noon: not in a grey-line window, but a next one exists (sunset).
        var noon = new DateTime(2026, 3, 20, 12, 0, 0, DateTimeKind.Utc);
        var st = GreyLine.StatusAt(0, 0, noon);
        Assert.False(st.IsActive);
        Assert.NotNull(st.Next);
        Assert.NotNull(st.UntilStart);
    }
}

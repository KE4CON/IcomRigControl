using IcomRigControl.Services;
using Xunit;

namespace IcomRigControl.Tests;

public class SwrProtectionTests
{
    [Theory]
    [InlineData(true, true, 4.0, 3.0, true)]    // enabled, transmitting, over threshold -> trip
    [InlineData(true, true, 2.5, 3.0, false)]   // under threshold -> no trip
    [InlineData(true, false, 9.0, 3.0, false)]  // not transmitting -> no trip
    [InlineData(false, true, 9.0, 3.0, false)]  // disabled -> no trip
    [InlineData(true, true, 3.0, 3.0, true)]    // exactly at threshold -> trip
    public void ShouldTrip_OnlyWhenEnabledTransmittingAndOverThreshold(
        bool enabled, bool transmitting, double swr, double threshold, bool expected) =>
        Assert.Equal(expected, SwrProtection.ShouldTrip(enabled, transmitting, swr, threshold));
}

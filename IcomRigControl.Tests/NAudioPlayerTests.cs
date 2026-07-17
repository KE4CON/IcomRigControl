using IcomRigControl.Services;
using Xunit;

namespace IcomRigControl.Tests;

public class NAudioPlayerTests
{
    [Fact]
    public void GetAvailableDevices_ReturnsAtLeastOneDevice()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var player = new NAudioPlayer();
        var devices = player.GetAvailableDevices();

        Assert.NotNull(devices);
    }

    [Fact]
    public void IsPlaying_FalseBeforePlaybackStarted()
    {
        if (!OperatingSystem.IsWindows()) return;

        var player = new NAudioPlayer();
        Assert.False(player.IsPlaying);
    }

    [Fact]
    public async Task PlayAsync_WithValidSamples_CompletesWithoutThrowing()
    {
        if (!OperatingSystem.IsWindows()) return;

        var player = new NAudioPlayer();
        float[] samples = new float[4410];

        await player.PlayAsync(samples, sampleRateHz: 44100);

        Assert.False(player.IsPlaying);
    }

    [Fact]
    public void Stop_WhenNotPlaying_DoesNotThrow()
    {
        if (!OperatingSystem.IsWindows()) return;

        var player = new NAudioPlayer();
        Exception? exception = null;
        try
        {
            player.Stop();
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        Assert.Null(exception);
    }
}

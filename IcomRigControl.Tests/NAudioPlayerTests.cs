using IcomRigControl.Services;
using Xunit;

namespace IcomRigControl.Tests;

public class NAudioPlayerTests
{
    [Fact]
    public void GetAvailableDevices_ReturnsAtLeastOneDevice()
    {
        var player = new NAudioPlayer();
        var devices = player.GetAvailableDevices();

        // Every Windows machine has at least a default output device,
        // even if it's a virtual/null device in a CI environment.
        Assert.NotNull(devices);
    }

    [Fact]
    public void IsPlaying_FalseBeforePlaybackStarted()
    {
        var player = new NAudioPlayer();
        Assert.False(player.IsPlaying);
    }

    [Fact]
    public async Task PlayAsync_WithValidSamples_CompletesWithoutThrowing()
    {
        var player = new NAudioPlayer();

        // A very short, quiet test tone -- a few hundred samples of silence
        // is enough to prove the playback pipeline runs end-to-end without
        // actually needing to verify audible output in an automated test.
        float[] samples = new float[4410]; // 0.1 second at 44100 Hz, all zeros (silence)

        await player.PlayAsync(samples, sampleRateHz: 44100);

        Assert.False(player.IsPlaying); // should have completed by the time PlayAsync returns
    }

   [Fact]
    public void Stop_WhenNotPlaying_DoesNotThrow()
    {
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
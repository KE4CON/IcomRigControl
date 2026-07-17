using IcomRigControl.Services;
using Xunit;

namespace IcomRigControl.Tests;

public class MacAudioPlayerTests
{
    [Fact]
    public void GetAvailableDevices_ReturnsSystemDefaultPlaceholder()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var player = new MacAudioPlayer();
        var devices = player.GetAvailableDevices();

        // afplay has no device-selection capability, so this always
        // returns a single "System Default" entry rather than a real
        // device enumeration -- documented limitation, not a bug.
        Assert.Single(devices);
        Assert.Equal("System Default (afplay)", devices[0]);
    }

    [Fact]
    public void IsPlaying_FalseBeforePlaybackStarted()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var player = new MacAudioPlayer();
        Assert.False(player.IsPlaying);
    }

    [Fact]
    public async Task PlayAsync_WithValidSamples_CompletesWithoutThrowing()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var player = new MacAudioPlayer();
        float[] samples = new float[4410]; // 0.1 second of silence at 44100 Hz

        await player.PlayAsync(samples, sampleRateHz: 44100);

        Assert.False(player.IsPlaying);
    }

    [Fact]
    public async Task PlayAsync_CleansUpTempWavFile()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var player = new MacAudioPlayer();
        float[] samples = new float[4410];

        int tempFilesBefore = System.IO.Directory.GetFiles(
            System.IO.Path.GetTempPath(), "icomrigcontrol_audio_*.wav").Length;

        await player.PlayAsync(samples, sampleRateHz: 44100);

        int tempFilesAfter = System.IO.Directory.GetFiles(
            System.IO.Path.GetTempPath(), "icomrigcontrol_audio_*.wav").Length;

        Assert.Equal(tempFilesBefore, tempFilesAfter);
    }

    [Fact]
    public void Stop_WhenNotPlaying_DoesNotThrow()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var player = new MacAudioPlayer();
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

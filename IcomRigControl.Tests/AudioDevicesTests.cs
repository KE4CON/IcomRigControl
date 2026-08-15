using System;
using IcomRigControl.Services;
using Xunit;

namespace IcomRigControl.Tests;

public class AudioDevicesTests
{
    [Fact]
    public void CreatePlayer_ReturnsThePlatformPlayer()
    {
        var player = AudioDevices.CreatePlayer();
        Assert.NotNull(player);

        if (OperatingSystem.IsWindows()) Assert.IsType<NAudioPlayer>(player);
        else if (OperatingSystem.IsMacOS()) Assert.IsType<MacAudioPlayer>(player);
        else Assert.IsType<LinuxAudioPlayer>(player);
    }

    [Fact]
    public void CreateCapture_ReturnsThePlatformCapture_AndStartsIdle()
    {
        var capture = AudioDevices.CreateCapture();
        Assert.NotNull(capture);
        Assert.False(capture.IsCapturing);

        if (OperatingSystem.IsWindows()) Assert.IsType<NAudioCapture>(capture);
        else if (OperatingSystem.IsLinux()) Assert.IsType<LinuxAudioCapture>(capture);
        else Assert.IsType<MacAudioCapture>(capture);
    }
}

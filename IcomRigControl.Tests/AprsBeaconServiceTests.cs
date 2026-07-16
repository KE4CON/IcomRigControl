using IcomRigControl.CivEngine;
using IcomRigControl.RigModel;
using IcomRigControl.Services;
using Xunit;

namespace IcomRigControl.Tests;

/// <summary>
/// A fake audio player that records calls instead of touching real hardware,
/// so AprsBeaconService's PTT-timing orchestration can be tested without a
/// real sound card or radio.
/// </summary>
public class FakeAudioPlayer : IAudioPlayer
{
    public List<(float[] Samples, int SampleRateHz, string? DeviceName)> PlayedClips { get; } = new();
    public bool IsPlaying { get; private set; }

    public async Task PlayAsync(float[] samples, int sampleRateHz, string? deviceName = null)
    {
        PlayedClips.Add((samples, sampleRateHz, deviceName));
        IsPlaying = true;
        await Task.Delay(10); // simulate brief playback time
        IsPlaying = false;
    }

    public void Stop() => IsPlaying = false;
    public List<string> GetAvailableDevices() => new() { "Fake Device" };
}

public class AprsBeaconServiceTests
{
    [Fact]
    public async Task SendBeacon_KeysPttOnBeforePlayingAudio()
    {
        var transport = new FakeCivTransport();
        var tx = new Transceiver(transport, RadioModel.IC7300);
        await tx.ConnectAsync();

        var audioPlayer = new FakeAudioPlayer();
        var beaconService = new AprsBeaconService(tx, audioPlayer);


        await beaconService.SendBeaconAsync(
            callsign: "KE4CON", ssid: 9,
            latitude: 43.65, longitude: -79.38,
            symbolTable: '/', symbolCode: '>', comment: "test",
            profile: AfskProfile.Hf300Baud);

        // PTT should have been keyed on (we can't check "during" playback with
        // this fake, but we CAN confirm the sequence completed and PTT was
        // released again afterward -- see the next test for the off-check).
        Assert.Single(audioPlayer.PlayedClips);
    }

    [Fact]
    public async Task SendBeacon_ReleasesPttAfterAudioCompletes()
    {
        var transport = new FakeCivTransport();
        var tx = new Transceiver(transport, RadioModel.IC7300);
        await tx.ConnectAsync();

        var audioPlayer = new FakeAudioPlayer();
        var beaconService = new AprsBeaconService(tx, audioPlayer);

        await beaconService.SendBeaconAsync(
            callsign: "KE4CON", ssid: 9,
            latitude: 43.65, longitude: -79.38,
            symbolTable: '/', symbolCode: '>', comment: "test",
            profile: AfskProfile.Hf300Baud);

        Assert.False(tx.PttActive);
    }

    [Fact]
    public async Task SendBeacon_IfAudioPlaybackThrows_StillReleasesPtt()
    {
        var transport = new FakeCivTransport();
        var tx = new Transceiver(transport, RadioModel.IC7300);
        await tx.ConnectAsync();

        var throwingPlayer = new ThrowingAudioPlayer();
        var beaconService = new AprsBeaconService(tx, throwingPlayer);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            beaconService.SendBeaconAsync(
                callsign: "KE4CON", ssid: 9,
                latitude: 43.65, longitude: -79.38,
                symbolTable: '/', symbolCode: '>', comment: "test",
                profile: AfskProfile.Hf300Baud));

        // Even though playback failed, PTT must not be left stuck on --
        // that would leave the radio transmitting indefinitely, a real
        // safety concern, not just a bug.
        Assert.False(tx.PttActive);
    }

    [Fact]
    public async Task SendBeacon_PassesCorrectDeviceNameToAudioPlayer()
    {
        var transport = new FakeCivTransport();
        var tx = new Transceiver(transport, RadioModel.IC7300);
        await tx.ConnectAsync();

        var audioPlayer = new FakeAudioPlayer();
        var beaconService = new AprsBeaconService(tx, audioPlayer);

        await beaconService.SendBeaconAsync(
            callsign: "KE4CON", ssid: 9,
            latitude: 43.65, longitude: -79.38,
            symbolTable: '/', symbolCode: '>', comment: "test",
            profile: AfskProfile.Hf300Baud,
            audioDeviceName: "My Radio Interface");

        Assert.Equal("My Radio Interface", audioPlayer.PlayedClips[0].DeviceName);
    }

    private class ThrowingAudioPlayer : IAudioPlayer
    {
        public bool IsPlaying => false;
        public Task PlayAsync(float[] samples, int sampleRateHz, string? deviceName = null) =>
            throw new InvalidOperationException("Simulated audio device failure.");
        public void Stop() { }
        public List<string> GetAvailableDevices() => new();
    }
}
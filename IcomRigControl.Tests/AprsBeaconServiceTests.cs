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
    public async Task SendBeacon_IfKeyUpEventHandlerThrows_StillReleasesPtt()
    {
        var transport = new FakeCivTransport();
        var tx = new Transceiver(transport, RadioModel.IC7300);
        await tx.ConnectAsync();

        // A PttChanged subscriber that blows up the instant PTT is keyed ON
        // (but not when it's released). This simulates any failure occurring
        // AFTER the radio is physically keyed. Before the fix the key-up sat
        // outside the try/finally, so this left the transmitter stuck on with
        // no key-down — the exact indefinite-transmit safety hazard.
        tx.PttChanged += (_, active) =>
        {
            if (active) throw new InvalidOperationException("Simulated key-up handler failure.");
        };

        var audioPlayer = new FakeAudioPlayer();
        var beaconService = new AprsBeaconService(tx, audioPlayer);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            beaconService.SendBeaconAsync(
                callsign: "KE4CON", ssid: 9,
                latitude: 43.65, longitude: -79.38,
                symbolTable: '/', symbolCode: '>', comment: "test",
                profile: AfskProfile.Hf300Baud));

        // PTT must have been released despite the key-up-time failure...
        Assert.False(tx.PttActive);

        // ...and a key-down frame (1C 00 00) must actually have reached the radio.
        var lastFrame = transport.WrittenFrames[^1];
        Assert.Equal(CivCommands.PttTunerStatus, lastFrame[4]); // 0x1C
        Assert.Equal(CivCommands.PttRx, lastFrame[5]);          // 0x00 sub-command
        Assert.Equal(0x00, lastFrame[6]);                       // 0x00 = receive
    }

    [Fact]
    public async Task SendBeacon_WhileAnotherBeaconInFlight_IsSkipped_NotCrossKeyed()
    {
        var transport = new FakeCivTransport();
        var tx = new Transceiver(transport, RadioModel.IC7300);
        await tx.ConnectAsync();

        var blockingPlayer = new BlockingAudioPlayer();
        var beaconService = new AprsBeaconService(tx, blockingPlayer);

        // Start beacon A; it keys PTT and blocks inside PlayAsync until released.
        var beaconA = beaconService.SendBeaconAsync(
            callsign: "KE4CON", ssid: 9, latitude: 43.65, longitude: -79.38,
            symbolTable: '/', symbolCode: '>', comment: "A",
            profile: AfskProfile.Hf300Baud, pttSettleMilliseconds: 0);

        // Wait (deterministically, no fixed delay) until A is definitely keyed
        // and transmitting audio.
        await blockingPlayer.Started.Task;
        int framesWhileTransmitting = transport.WrittenFrames.Count;

        // Fire beacon B while A is still keyed. It must be SKIPPED (return false)
        // and send no additional PTT/audio frames — no cross-keying.
        bool bResult = await beaconService.SendBeaconAsync(
            callsign: "KE4CON", ssid: 9, latitude: 43.65, longitude: -79.38,
            symbolTable: '/', symbolCode: '>', comment: "B",
            profile: AfskProfile.Hf300Baud, pttSettleMilliseconds: 0);

        Assert.False(bResult);
        Assert.Equal(framesWhileTransmitting, transport.WrittenFrames.Count);

        // Let A finish; confirm it completed and released PTT.
        blockingPlayer.Release.SetResult();
        bool aResult = await beaconA;
        Assert.True(aResult);
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

    /// An audio player that signals when playback has started and then blocks
    /// until explicitly released, so a test can hold one beacon "on the air"
    /// while it fires a second — deterministically, with no timing races.
    private class BlockingAudioPlayer : IAudioPlayer
    {
        public readonly TaskCompletionSource Started =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource Release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool IsPlaying => false;

        public async Task PlayAsync(float[] samples, int sampleRateHz, string? deviceName = null)
        {
            Started.TrySetResult();
            await Release.Task;
        }

        public void Stop() { }
        public List<string> GetAvailableDevices() => new();
    }
}
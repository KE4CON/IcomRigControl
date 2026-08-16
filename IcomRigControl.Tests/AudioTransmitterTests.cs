using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IcomRigControl.RigModel;
using IcomRigControl.Services;
using Xunit;

namespace IcomRigControl.Tests;

public class AudioTransmitterTests
{
    [Fact]
    public async Task Transmit_KeysPtt_Plays_ThenUnkeys()
    {
        var rig = new Transceiver(new FakeCivTransport(), RadioModel.IC7300);
        var player = new RecordingPlayer { Rig = rig };
        var tx = new AudioTransmitter(rig, player);

        bool sent = await tx.TransmitAsync(new float[4410], 44100, null, pttSettleMilliseconds: 0);

        Assert.True(sent);
        Assert.True(player.Played, "audio should have been played");
        Assert.True(player.WasKeyedWhilePlaying, "PTT must be keyed during playback");
        Assert.False(rig.PttActive, "PTT must be released after transmit");
    }

    [Fact]
    public async Task Transmit_DoesNotKey_WhenInhibited()
    {
        var rig = new Transceiver(new FakeCivTransport(), RadioModel.IC7300) { TransmitInhibited = true };
        var player = new RecordingPlayer { Rig = rig };
        var tx = new AudioTransmitter(rig, player);

        await tx.TransmitAsync(new float[4410], 44100, null, pttSettleMilliseconds: 0);

        Assert.False(player.WasKeyedWhilePlaying, "TX-inhibit must prevent keying");
        Assert.False(rig.PttActive);
    }

    private sealed class RecordingPlayer : IAudioPlayer
    {
        public bool Played { get; private set; }
        public bool WasKeyedWhilePlaying { get; private set; }
        public Transceiver? Rig { get; set; } // observed so we can assert keying during playback
        public bool IsPlaying => false;

        public Task PlayAsync(float[] samples, int sampleRateHz, string? deviceName = null)
        {
            Played = true;
            if (Rig is not null) WasKeyedWhilePlaying = Rig.PttActive;
            return Task.CompletedTask;
        }

        public void Stop() { }
        public List<string> GetAvailableDevices() => new();
    }
}

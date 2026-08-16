using System.Collections.Generic;
using System.Threading.Tasks;
using IcomRigControl.CivEngine;
using IcomRigControl.RigModel;
using IcomRigControl.Services;
using Xunit;

namespace IcomRigControl.Tests;

public class LiveRttyTransmitterTests
{
    [Fact]
    public void BuildChunk_ProducesText_AndIdle()
    {
        short[] text = LiveRttyTransmitter.BuildChunk("CQ", RttyProfile.Hf45Baud, 44100);
        short[] idle = LiveRttyTransmitter.BuildChunk(null, RttyProfile.Hf45Baud, 44100);
        Assert.True(text.Length > 0);
        Assert.True(idle.Length > 0);
    }

    [Fact]
    public async Task Start_DoesNotKey_WhenInhibited()
    {
        var rig = new Transceiver(new FakeCivTransport(), RadioModel.IC7300) { TransmitInhibited = true };
        var tx = new LiveRttyTransmitter(rig, new NullStreamOutput());
        bool started = await tx.StartAsync();
        Assert.False(started);
        Assert.False(rig.PttActive);
        Assert.False(tx.IsTransmitting);
    }

    [Fact]
    public async Task Start_KeysAndStreams_ThenStopUnkeys()
    {
        var rig = new Transceiver(new FakeCivTransport(), RadioModel.IC7300);
        var output = new NullStreamOutput();
        var tx = new LiveRttyTransmitter(rig, output);

        Assert.True(await tx.StartAsync());
        Assert.True(rig.PttActive);
        tx.Enqueue("TEST");
        await Task.Delay(350); // let the pump run for a bit
        Assert.True(output.WriteCount > 0, "should be streaming audio while transmitting");
        // The loop must be PACED to real time, not spinning: a busy-loop (the bug)
        // would write many thousands of chunks in 350 ms. Paced ≈ a handful.
        Assert.True(output.WriteCount < 50, $"pump should be paced, not busy-looping (wrote {output.WriteCount})");

        await tx.StopAsync();
        Assert.False(rig.PttActive);
        Assert.False(tx.IsTransmitting);
    }

    private sealed class NullStreamOutput : IAudioStreamOutput
    {
        public int WriteCount { get; private set; }
        public void Start(int sampleRateHz, string? deviceName = null) { }
        public void Write(short[] pcmFrame) => WriteCount++;
        public void Stop() { }
    }
}

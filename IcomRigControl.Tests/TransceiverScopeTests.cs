using System;
using System.Threading.Tasks;
using IcomRigControl.CivEngine;
using IcomRigControl.RigModel;
using Xunit;

namespace IcomRigControl.Tests;

public class TransceiverScopeTests
{
    [Fact]
    public async Task StartScopeAsync_SendsOnAndWaveformOutputCommands()
    {
        var transport = new FakeCivTransport();
        var tx = new Transceiver(transport, RadioModel.IC7300);
        await tx.ConnectAsync();

        await tx.StartScopeAsync(TimeSpan.FromMilliseconds(100));

        Assert.True(tx.IsScopeRunning);
        Assert.True(transport.WrittenFrames.Count >= 2);

        tx.StopScope();
    }

    [Fact]
    public async Task IncomingWaveformFrame_UpdatesLastWaveformAndFiresEvent()
    {
        var transport = new FakeCivTransport();
        var tx = new Transceiver(transport, RadioModel.IC7300);

        int[]? received = null;
        tx.WaveformUpdated += (_, waveform) => received = waveform;

        // Simulate a small waveform response: FE FE E0 94 27 00 [3 data bytes] FD
        var incoming = new byte[] { 0xFE, 0xFE, 0xE0, 0x94, 0x27, 0x00, 0x10, 0x20, 0x30, 0xFD };
        transport.SimulateIncoming(incoming);

        Assert.NotNull(received);
        Assert.Equal(3, received!.Length);
        Assert.Equal(0x10, received[0]);
        Assert.Equal(0x20, received[1]);
        Assert.Equal(0x30, received[2]);
        Assert.Equal(received, tx.LastWaveform);
    }

    [Fact]
    public async Task StopScope_SetsIsScopeRunningFalse()
    {
        var transport = new FakeCivTransport();
        var tx = new Transceiver(transport, RadioModel.IC7300);
        await tx.ConnectAsync();

        await tx.StartScopeAsync(TimeSpan.FromMilliseconds(100));
        tx.StopScope();

        Assert.False(tx.IsScopeRunning);
    }
}
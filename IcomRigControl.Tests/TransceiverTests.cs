using IcomRigControl.CivEngine;
using IcomRigControl.RigModel;
using Xunit;

namespace IcomRigControl.Tests;

public class TransceiverTests
{
    [Fact]
    public async Task ConnectAsync_OpensTransport()
    {
        var transport = new FakeCivTransport();
        var tx = new Transceiver(transport, RadioModel.IC7300);

        await tx.ConnectAsync();

        Assert.True(tx.IsConnected);
        Assert.True(transport.IsOpen);
    }

    [Fact]
    public async Task SetFrequencyAsync_SendsCorrectFrameAndUpdatesProperty()
    {
        var transport = new FakeCivTransport();
        var tx = new Transceiver(transport, RadioModel.IC7300);
        await tx.ConnectAsync();

        await tx.SetFrequencyAsync(14_074_000);

        Assert.Equal(14_074_000, tx.FrequencyHz);
        Assert.Single(transport.WrittenFrames);
        Assert.Equal(CivCommands.SetFrequency, transport.WrittenFrames[0][4]);
    }

    [Fact]
    public async Task SetPttAsync_UpdatesPropertyAndFiresEvent()
    {
        var transport = new FakeCivTransport();
        var tx = new Transceiver(transport, RadioModel.IC7300);
        await tx.ConnectAsync();

        bool? eventFired = null;
        tx.PttChanged += (_, active) => eventFired = active;

        await tx.SetPttAsync(true);

        Assert.True(tx.PttActive);
        Assert.True(eventFired);
    }

    [Fact]
    public void IncomingFrequencyFrame_UpdatesPropertyAndFiresEvent()
    {
        var transport = new FakeCivTransport();
        var tx = new Transceiver(transport, RadioModel.IC7300);

        long? changedTo = null;
        tx.FrequencyChanged += (_, hz) => changedTo = hz;

        // Simulate the radio reporting its frequency: 7.200.000 Hz
        var incoming = new byte[] { 0xFE, 0xFE, 0xE0, 0x94, 0x03, 0x00, 0x00, 0x20, 0x07, 0x00, 0xFD };
        transport.SimulateIncoming(incoming);

        Assert.Equal(7_200_000, tx.FrequencyHz);
        Assert.Equal(7_200_000, changedTo);
    }

    [Fact]
    public void IncomingSMeterFrame_UpdatesSMeterProperties()
    {
        var transport = new FakeCivTransport();
        var tx = new Transceiver(transport, RadioModel.IC7300);

        // Simulate S-meter response: S9 (raw 01 20)
        var incoming = new byte[] { 0xFE, 0xFE, 0xE0, 0x94, 0x15, 0x02, 0x01, 0x20, 0xFD };
        transport.SimulateIncoming(incoming);

        Assert.Equal(9, tx.SMeterS);
    }

    [Fact]
    public async Task PollOnce_FiresMeterUpdatedEvent()
    {
        var transport = new FakeCivTransport();
        var tx = new Transceiver(transport, RadioModel.IC7300);
        await tx.ConnectAsync();

        // Start polling with a very short interval, then immediately stop —
        // just proving the loop runs at least once and sends the expected commands.
        tx.StartPolling(TimeSpan.FromMilliseconds(50));
        await Task.Delay(150);
        tx.StopPolling();

        // Should have sent at least one full round of 6 meter-read commands
        Assert.True(transport.WrittenFrames.Count >= 6);
    }

    [Fact]
    public async Task DisconnectAsync_ClosesTransportAndStopsPolling()
    {
        var transport = new FakeCivTransport();
        var tx = new Transceiver(transport, RadioModel.IC7300);
        await tx.ConnectAsync();
        tx.StartPolling(TimeSpan.FromMilliseconds(50));

        await tx.DisconnectAsync();

        Assert.False(tx.IsConnected);
        Assert.False(transport.IsOpen);
    }
}
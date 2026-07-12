using IcomRigControl.CivEngine;
using IcomRigControl.RigModel;
using Xunit;

namespace IcomRigControl.Tests;

public class TransceiverMemoryTests
{
    [Fact]
    public async Task ReadMemoryChannelAsync_SelectsChannelThenReadsFreqAndMode()
    {
        var transport = new FakeCivTransport();
        var tx = new Transceiver(transport, RadioModel.IC7300);
        await tx.ConnectAsync();

        // Kick off the read (don't await yet, we need to inject responses first)
        var readTask = tx.ReadMemoryChannelAsync(5);

        // Give the select-channel command time to be "sent"
        await Task.Delay(100);

        // Simulate the radio replying to the frequency read: 7.200.000 Hz
        transport.SimulateIncoming(new byte[]
            { 0xFE, 0xFE, 0xE0, 0x94, 0x03, 0x00, 0x00, 0x20, 0x07, 0x00, 0xFD });

        // Simulate the radio replying to the mode read: USB
        transport.SimulateIncoming(new byte[]
            { 0xFE, 0xFE, 0xE0, 0x94, 0x04, 0x01, 0x01, 0xFD });

        var result = await readTask;

        Assert.NotNull(result);
        Assert.Equal(5, result!.ChannelNumber);
        Assert.Equal(7_200_000, result.FrequencyHz);
        Assert.Equal("USB", result.Mode);
    }

    [Fact]
    public async Task ReadMemoryChannelAsync_NoResponse_ReturnsNull()
    {
        var transport = new FakeCivTransport();
        var tx = new Transceiver(transport, RadioModel.IC7300);
        await tx.ConnectAsync();

        // No simulated response at all — channel is "empty"
        var result = await tx.ReadMemoryChannelAsync(50);

        Assert.Null(result);
    }

    [Fact]
    public async Task WriteMemoryChannelAsync_SendsSelectFrequencyAndMode()
    {
        var transport = new FakeCivTransport();
        var tx = new Transceiver(transport, RadioModel.IC7300);
        await tx.ConnectAsync();

        var channel = new MemoryChannel(10, 14_074_000, "USB", "FT8 20m");
        await tx.WriteMemoryChannelAsync(channel);

        // Should have sent: select channel, set frequency, set mode = 3 frames minimum
        Assert.True(transport.WrittenFrames.Count >= 3);

        // First frame should be the channel select (command 08h)
        Assert.Equal(CivCommands.SelectMemory, transport.WrittenFrames[0][4]);
    }

    [Fact]
    public void MemoryChannel_Empty_CreatesPlaceholderWithChannelNumber()
    {
        var empty = MemoryChannel.Empty(7);

        Assert.Equal(7, empty.ChannelNumber);
        Assert.Equal(0, empty.FrequencyHz);
        Assert.Equal(string.Empty, empty.Name);
    }
}
using IcomRigControl.CivEngine;
using Xunit;

namespace IcomRigControl.Tests;

/// <summary>
/// A fake transport that lets tests simulate a radio's responses without hardware.
/// </summary>
public class FakeCivTransport : ICivTransport
{
    public bool IsOpen { get; private set; }
    public List<byte[]> WrittenFrames { get; } = new();
    public event EventHandler<byte[]>? DataReceived;

    public Task OpenAsync(CancellationToken ct = default)
    {
        IsOpen = true;
        return Task.CompletedTask;
    }

    public Task CloseAsync()
    {
        IsOpen = false;
        return Task.CompletedTask;
    }

    public Task WriteAsync(byte[] data, CancellationToken ct = default)
    {
        WrittenFrames.Add(data);
        return Task.CompletedTask;
    }

    /// Simulate the radio sending bytes back (test helper, not part of the interface)
    public void SimulateIncoming(byte[] data)
    {
        DataReceived?.Invoke(this, data);
    }

    public ValueTask DisposeAsync()
    {
        IsOpen = false;
        return ValueTask.CompletedTask;
    }
}

public class EndToEndPipelineTests
{
    [Fact]
    public async Task SetFrequency_SendsCorrectBytesOverTransport()
    {
        var transport = new FakeCivTransport();
        await transport.OpenAsync();

        var builder = new CivFrameBuilder(CivCommands.Addr7300);
        var frame = builder.SetFrequency(14_074_000);

        await transport.WriteAsync(frame);

        Assert.Single(transport.WrittenFrames);
        var sent = transport.WrittenFrames[0];

        // FE FE 94 E0 05 [5 BCD bytes] FD = 11 bytes
        Assert.Equal(11, sent.Length);
        Assert.Equal(0xFE, sent[0]);
        Assert.Equal(0xFE, sent[1]);
        Assert.Equal(CivCommands.Addr7300, sent[2]);
        Assert.Equal(CivCommands.AddrController, sent[3]);
        Assert.Equal(CivCommands.SetFrequency, sent[4]);
        Assert.Equal(0xFD, sent[^1]);
    }

    [Fact]
    public void SimulatedRadioResponse_ParsesCorrectly()
    {
        var transport = new FakeCivTransport();
        var parser = new CivFrameParser();
        CivFrame? received = null;

        transport.DataReceived += (_, bytes) =>
        {
            var frames = parser.Feed(bytes);
            if (frames.Count > 0) received = frames[0];
        };

        // Simulate the IC-7300 responding to a frequency read with 14.074.000 Hz
        var simulatedResponse = new byte[] { 0xFE, 0xFE, 0xE0, 0x94, 0x03, 0x00, 0x40, 0x07, 0x14, 0x00, 0xFD };
        transport.SimulateIncoming(simulatedResponse);

        Assert.NotNull(received);
        Assert.Equal(CivCommands.ReadFrequency, received!.Command);

        var freq = BcdCodec.DecodeFrequency(received.Data);
        Assert.Equal(14_074_000, freq);
    }

    [Fact]
    public async Task FullRoundTrip_BuildSendReceiveDecode()
    {
        // This simulates the complete flow: build a command, "send" it,
        // radio "responds", parse response, decode the value.
        var transport = new FakeCivTransport();
        await transport.OpenAsync();

        var builder = new CivFrameBuilder(CivCommands.Addr7300);
        var parser = new CivFrameParser();

        // 1. Build and send a "read frequency" request
        var request = builder.ReadFrequency();
        await transport.WriteAsync(request);
        Assert.Single(transport.WrittenFrames);

        // 2. Simulate radio replying with 7.200.000 Hz (40m phone)
        var reply = new byte[] { 0xFE, 0xFE, 0xE0, 0x94, 0x03, 0x00, 0x00, 0x20, 0x07, 0x00, 0xFD };
        var frames = parser.Feed(reply);

        // 3. Confirm we can decode it back to the original frequency
        Assert.Single(frames);
        var decoded = BcdCodec.DecodeFrequency(frames[0].Data);
        Assert.Equal(7_200_000, decoded);
    }
}

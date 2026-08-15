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

    [Theory]
    [InlineData(RadioModel.IC7300, 0x94)]
    [InlineData(RadioModel.IC7300MK2, 0xB6)]
    [InlineData(RadioModel.IC705, 0xA4)]
    public async Task Transceiver_AddressesOutgoingFramesToTheSelectedRadio(RadioModel model, byte expectedAddress)
    {
        var transport = new FakeCivTransport();
        var tx = new Transceiver(transport, model);
        await tx.ConnectAsync();

        await tx.SetFrequencyAsync(14_074_000);

        // Byte [2] of a CI-V frame is the destination (radio) address:
        // FE FE <to> <from> ... Each supported radio has its own default address.
        Assert.Equal(expectedAddress, transport.WrittenFrames[0][2]);
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
    public async Task PowerOffAsync_SendsPowerOffFrame()
    {
        var transport = new FakeCivTransport();
        var tx = new Transceiver(transport, RadioModel.IC7300);
        await tx.ConnectAsync();

        await tx.PowerOffAsync();

        // FE FE 94 E0 18 00 FD
        Assert.Equal(new byte[] { 0xFE, 0xFE, 0x94, 0xE0, 0x18, 0x00, 0xFD }, transport.WrittenFrames[0]);
    }

    [Fact]
    public async Task PowerOnAsync_SendsWakeupPreambleThenPowerOnFrame()
    {
        var transport = new FakeCivTransport();
        var tx = new Transceiver(transport, RadioModel.IC7300);
        await tx.ConnectAsync();

        await tx.PowerOnAsync();

        var frame = transport.WrittenFrames[0];

        // The real command frame is the last 7 bytes: FE FE 94 E0 18 01 FD
        Assert.Equal(new byte[] { 0xFE, 0xFE, 0x94, 0xE0, 0x18, 0x01, 0xFD }, frame[^7..]);

        // ...preceded by a wake-up preamble of 0xFE bytes to rouse the radio's
        // CI-V from standby (all leading bytes before the frame are 0xFE).
        Assert.True(frame.Length > 7);
        Assert.All(frame[..^7], b => Assert.Equal(0xFE, b));
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
    public void EchoedReadFrequencyRequest_IsIgnored_AndDoesNotCrashReceiveLoop()
    {
        var transport = new FakeCivTransport();
        var tx = new Transceiver(transport, RadioModel.IC7300);

        bool freqChangedFired = false;
        tx.FrequencyChanged += (_, _) => freqChangedFired = true;

        // The radio echoes our own ReadFrequency request straight back on the
        // CI-V bus: FE FE 94 E0 03 FD (To=radio, From=controller, cmd=03, NO
        // data payload). Before the fix this reached BcdCodec.DecodeFrequency
        // with an empty span and threw IndexOutOfRangeException, which escaped
        // ReadLoopAsync and silently killed all further reception. It must now
        // be ignored (echo from the controller address), leaving state untouched.
        var echo = new byte[] { 0xFE, 0xFE, 0x94, 0xE0, 0x03, 0xFD };
        var ex = Record.Exception(() => transport.SimulateIncoming(echo));

        Assert.Null(ex);
        Assert.False(freqChangedFired);
        Assert.Equal(0, tx.FrequencyHz);
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
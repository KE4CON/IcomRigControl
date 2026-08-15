using IcomRigControl.CivEngine;
using Xunit;

namespace IcomRigControl.Tests;

/// <summary>
/// The receive pipeline (AfskDemodulator -> HdlcDeframer -> Ax25Decoder) is the
/// exact inverse of the transmit pipeline (Ax25FrameBuilder -> ModulateAx25Frame).
/// Feeding the modulator's own output back through the receiver must recover the
/// original frame — a full end-to-end proof with no hardware.
/// </summary>
public class AprsReceiveRoundTripTests
{
    [Theory]
    [InlineData(44100)]
    [InlineData(48000)]
    public void ModulateThenDemodulate_RecoversTheAprsFrame(int sampleRate)
    {
        const string info = "!4903.50N/07201.75W-Test HF APRS 300 baud";
        var frameBytes = Ax25FrameBuilder.BuildUiFrame(
            sourceCallsign: "KE4CON", sourceSsid: 9,
            destinationCallsign: "APRS", destinationSsid: 0,
            infoField: info);

        float[] audio = AfskModulator.ModulateAx25Frame(frameBytes, AfskProfile.Hf300Baud, sampleRate);

        var bits = AfskDemodulator.Demodulate(audio, AfskProfile.Hf300Baud, sampleRate);
        var frames = HdlcDeframer.ExtractFrames(bits);

        Assert.NotEmpty(frames); // FCS-valid frame recovered

        var decoded = Ax25Decoder.Decode(frames[0]);
        Assert.NotNull(decoded);
        Assert.Equal("KE4CON", decoded!.Source);
        Assert.Equal(9, decoded.SourceSsid);
        Assert.Equal("APRS", decoded.Destination);
        Assert.Equal(info, decoded.InfoField);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(37)]    // arbitrary sub-bit offset
    [InlineData(200)]   // more than one bit period
    [InlineData(1000)]
    public void AprsReceiver_DecodesAPacketAtAnyBufferOffset_ViaBitSync(int leadingSilenceSamples)
    {
        const int sampleRate = 44100;
        var frameBytes = Ax25FrameBuilder.BuildUiFrame("KE4CON", 7, "APRS", 0, "!4903.50N/07201.75W-mobile");
        float[] packet = AfskModulator.ModulateAx25Frame(frameBytes, AfskProfile.Hf300Baud, sampleRate);

        // Place the packet after some leading silence, so it does NOT start on a
        // bit boundary — the receiver's phase sweep must still find it.
        var buffer = new float[leadingSilenceSamples + packet.Length + 500];
        System.Array.Copy(packet, 0, buffer, leadingSilenceSamples, packet.Length);

        var received = AprsReceiver.DecodePackets(buffer, AfskProfile.Hf300Baud, sampleRate);

        Assert.Single(received); // exactly one, deduped across phases
        Assert.Equal("KE4CON", received[0].Frame.Source);
        Assert.Equal(AprsPacketType.Position, received[0].Packet.Type);
        Assert.Equal(49.0583, received[0].Packet.Latitude!.Value, 3);
    }

    [Fact]
    public void Demodulate_RoundTripsPcmShorts_Too()
    {
        var frameBytes = Ax25FrameBuilder.BuildUiFrame("W1AW", 0, "APRS", 0, ">Hello from HF");
        float[] audio = AfskModulator.ModulateAx25Frame(frameBytes, AfskProfile.Hf300Baud, 44100);

        // Convert to 16-bit PCM (what IAudioCapture delivers) and decode that.
        var pcm = new short[audio.Length];
        for (int i = 0; i < audio.Length; i++) pcm[i] = (short)(audio[i] * 32767);

        var frames = HdlcDeframer.ExtractFrames(AfskDemodulator.Demodulate(pcm, AfskProfile.Hf300Baud, 44100));
        Assert.NotEmpty(frames);
        Assert.Equal("W1AW", Ax25Decoder.Decode(frames[0])!.Source);
    }
}

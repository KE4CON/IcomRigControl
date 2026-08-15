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

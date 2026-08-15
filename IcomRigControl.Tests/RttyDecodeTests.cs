using IcomRigControl.CivEngine;
using Xunit;

namespace IcomRigControl.Tests;

public class BaudotCodeTests
{
    [Fact]
    public void Decode_LettersAndFigures_SwitchOnShift()
    {
        // "00001" is E in letters, 3 in figures.
        Assert.Equal('E', BaudotCode.Decode("00001", figures: false));
        Assert.Equal('3', BaudotCode.Decode("00001", figures: true));
    }

    [Fact]
    public void Encode_InsertsShiftCodes_ForDigits()
    {
        var codes = BaudotCode.Encode("A1");
        // A (letter), then FIGS shift, then 1.
        Assert.Equal(BaudotCode.Encode("A")[0], codes[0]);
        Assert.Contains(BaudotCode.Figs, codes);
    }

    [Fact]
    public void Encode_SpaceIsShiftIndependent()
    {
        var codes = BaudotCode.Encode("A B");
        Assert.Contains(BaudotCode.Space, codes);
    }
}

public class RttyDecoderRoundTripTests
{
    [Theory]
    [InlineData(44100)]
    [InlineData(48000)]
    public void ModulateThenDecode_LettersRoundTrip(int sampleRate)
    {
        const string message = "CQ CQ DE KE4CON KE4CON";
        float[] audio = RttyModulator.Modulate(message, RttyProfile.Hf45Baud, sampleRate);
        string decoded = RttyDecoder.Decode(audio, RttyProfile.Hf45Baud, sampleRate);
        Assert.Equal(message, decoded.Trim());
    }

    [Fact]
    public void ModulateThenDecode_WithFiguresRoundTrips()
    {
        const string message = "RST 599 DE KE4CON";
        float[] audio = RttyModulator.Modulate(message, RttyProfile.Hf45Baud, 44100);
        string decoded = RttyDecoder.Decode(audio, RttyProfile.Hf45Baud, 44100);
        Assert.Equal(message, decoded.Trim());
    }

    [Fact]
    public void Reverse_DecodesWhenMarkSpaceSwapped()
    {
        const string message = "TEST DE KE4CON";
        // Modulate normally, but build a profile with the tones swapped to simulate
        // a reversed (wrong-sideband) signal — the reverse flag should recover it.
        var swapped = new RttyProfile(RttyProfile.Hf45Baud.SpaceFrequencyHz,
                                      RttyProfile.Hf45Baud.MarkFrequencyHz, RttyProfile.Hf45Baud.BaudRate);
        float[] audio = RttyModulator.Modulate(message, swapped, 44100);

        string normal = RttyDecoder.Decode(audio, RttyProfile.Hf45Baud, 44100, reverse: false);
        string reversed = RttyDecoder.Decode(audio, RttyProfile.Hf45Baud, 44100, reverse: true);

        Assert.NotEqual(message, normal.Trim());       // wrong polarity is garbled
        Assert.Equal(message, reversed.Trim());         // reverse flag fixes it
    }
}

using IcomRigControl.CivEngine;
using Xunit;

namespace IcomRigControl.Tests;

public class MorseCodeTests
{
    [Theory]
    [InlineData(".-", 'A')]
    [InlineData("-...", 'B')]
    [InlineData("...", 'S')]
    [InlineData("-", 'T')]
    [InlineData(".----", '1')]
    [InlineData("-----", '0')]
    [InlineData("-..-.", '/')]
    [InlineData("-...-", '=')]
    public void Decode_KnownPatterns(string pattern, char expected) =>
        Assert.Equal(expected, MorseCode.Decode(pattern));

    [Fact]
    public void Decode_UnknownPattern_IsNull() =>
        Assert.Null(MorseCode.Decode("........")); // 8 dits is not a character

    [Theory]
    [InlineData('a', ".-")]
    [InlineData('Q', "--.-")]
    [InlineData('9', "----.")]
    public void Encode_IsCaseInsensitiveAndCorrect(char c, string expected) =>
        Assert.Equal(expected, MorseCode.Encode(c));

    [Fact]
    public void EncodeThenDecode_RoundTripsEveryLetterAndDigit()
    {
        for (char c = 'A'; c <= 'Z'; c++)
            Assert.Equal(c, MorseCode.Decode(MorseCode.Encode(c)!));
        for (char c = '0'; c <= '9'; c++)
            Assert.Equal(c, MorseCode.Decode(MorseCode.Encode(c)!));
    }
}

public class MorseDecoderTests
{
    // Feed hand-built timing at a known dot length: dit=1u, dah=3u, gaps 1/3/7u.
    [Fact]
    public void DecodesParis_FromExactTiming()
    {
        var d = new MorseDecoder(initialWpm: 20);
        double u = 1.2 / 20.0; // one unit in seconds
        var sb = new System.Text.StringBuilder();

        // "PARIS": P=.--. A=.- R=.-. I=.. S=...
        string[] letters = { ".--.", ".-", ".-.", "..", "..." };
        for (int li = 0; li < letters.Length; li++)
        {
            string pat = letters[li];
            for (int i = 0; i < pat.Length; i++)
            {
                if (i > 0) sb.Append(d.Add(false, 1 * u));         // element gap
                sb.Append(d.Add(true, (pat[i] == '-' ? 3 : 1) * u)); // element
            }
            if (li < letters.Length - 1) sb.Append(d.Add(false, 3 * u)); // char gap
        }
        sb.Append(d.Flush());

        Assert.Equal("PARIS", sb.ToString());
    }

    [Fact]
    public void InsertsSpace_OnWordGap()
    {
        var d = new MorseDecoder(20);
        double u = 1.2 / 20.0;
        var sb = new System.Text.StringBuilder();
        sb.Append(d.Add(true, 1 * u));   // E = .
        sb.Append(d.Add(false, 7 * u));  // word gap
        sb.Append(d.Add(true, 1 * u));   // E = .
        sb.Append(d.Flush());
        Assert.Equal("E E", sb.ToString());
    }
}

public class CwDecoderRoundTripTests
{
    [Theory]
    [InlineData(44100)]
    [InlineData(48000)]
    public void ModulateThenDecode_RoundTripsAtCommonSampleRates(int sampleRate)
    {
        const string message = "CQ TEST DE KE4CON";
        float[] audio = CwModulator.Modulate(message, wpm: 20, pitchHz: 600, sampleRateHz: sampleRate);

        // Pad with lead/trail silence, as a real capture would have.
        var padded = new float[sampleRate / 2 + audio.Length + sampleRate / 2];
        System.Array.Copy(audio, 0, padded, sampleRate / 2, audio.Length);

        string decoded = CwDecoder.Decode(padded, pitchHz: 600, sampleRateHz: sampleRate, initialWpm: 20);
        Assert.Equal(message, decoded.Trim());
    }

    [Fact]
    public void Decode_TracksASpeedItWasNotSeededFor()
    {
        // Seeded for 20 WPM but sent at 25 — after locking, the body should copy.
        const string message = "PARIS PARIS";
        float[] audio = CwModulator.Modulate(message, wpm: 25, pitchHz: 700, sampleRateHz: 44100);
        string decoded = CwDecoder.Decode(audio, pitchHz: 700, sampleRateHz: 44100, initialWpm: 20).Trim();
        Assert.Contains("PARIS", decoded);
    }
}

public class CwPitchMeterTests
{
    [Theory]
    [InlineData(600)]
    [InlineData(750)]
    [InlineData(430)]
    public void MeasuresToneNearTruth(double toneHz)
    {
        // A steady tone at toneHz; search centered on 600 with a 400 Hz span.
        float[] audio = CwModulator.Modulate("T", wpm: 8, pitchHz: toneHz, sampleRateHz: 44100);
        double? measured = CwPitchMeter.MeasureToneHz(audio, 44100, centerHz: 600, spanHz: 400, stepHz: 10);
        Assert.NotNull(measured);
        Assert.True(System.Math.Abs(measured!.Value - toneHz) < 12,
            $"measured {measured} should be within ~12 Hz of {toneHz}");
    }

    [Fact]
    public void ReturnsNull_OnSilence()
    {
        var silence = new float[44100 / 10];
        Assert.Null(CwPitchMeter.MeasureToneHz(silence, 44100));
    }
}

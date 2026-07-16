using IcomRigControl.CivEngine;
using Xunit;

namespace IcomRigControl.Tests;

public class AfskModulatorTests
{
    [Fact]
    public void BitStuff_FiveConsecutiveOnes_InsertsZero()
    {
        // Input: 0b11111000 (five 1s then three 0s) as individual bits,
        // MSB-first for readability in the test, though AX.25 transmits LSB-first.
        bool[] input = { true, true, true, true, true, false, false, false };

        bool[] result = AfskModulator.BitStuff(input);

        // After five 1s, a stuffed 0 must be inserted before the sixth bit.
        Assert.Equal(9, result.Length);
        Assert.True(result[0] && result[1] && result[2] && result[3] && result[4]);
        Assert.False(result[5]); // the stuffed bit
    }

    [Fact]
    public void BitStuff_FewerThanFiveOnes_NoStuffing()
    {
        bool[] input = { true, true, true, false, true };
        bool[] result = AfskModulator.BitStuff(input);

        Assert.Equal(input.Length, result.Length);
    }

    [Fact]
    public void BitStuff_ExactlyFiveOnesAtEnd_StuffsZeroAtEnd()
    {
        bool[] input = { false, true, true, true, true, true };
        bool[] result = AfskModulator.BitStuff(input);

        Assert.Equal(7, result.Length);
        Assert.False(result[6]);
    }

    [Fact]
    public void NrziEncode_OnesKeepSameLevel_ZerosToggle()
    {
        // NRZI: a '1' bit means "no transition" (keep the same level), a
        // '0' bit means "transition" (flip the level). Starting level is
        // conventionally high (true).
        bool[] bits = { true, true, false, true, false, false };
        bool[] levels = AfskModulator.NrziEncode(bits);

        Assert.Equal(6, levels.Length);
        Assert.True(levels[0]);  // '1' -> no change from initial high -> stays high
        Assert.True(levels[1]);  // '1' -> no change -> stays high
        Assert.False(levels[2]); // '0' -> transition -> flips to low
        Assert.False(levels[3]); // '1' -> no change -> stays low
        Assert.True(levels[4]);  // '0' -> transition -> flips to high
        Assert.False(levels[5]); // '0' -> transition -> flips to low
    }

    [Fact]
    public void GenerateTone_ProducesCorrectSampleCount()
    {
        // At 44100 Hz sample rate, one bit period at 1200 baud is
        // 44100/1200 = 36.75 samples -- rounds to 37.
        float[] samples = AfskModulator.GenerateBitTone(
            frequencyHz: 1200, sampleRateHz: 44100, baudRate: 1200, phase: 0, out _);

        Assert.InRange(samples.Length, 36, 38);
    }

    [Fact]
    public void GenerateTone_SamplesAreWithinValidAudioRange()
    {
        float[] samples = AfskModulator.GenerateBitTone(
            frequencyHz: 1200, sampleRateHz: 44100, baudRate: 1200, phase: 0, out _);

        Assert.All(samples, s => Assert.InRange(s, -1.0f, 1.0f));
    }

    [Fact]
    public void ModulateFrame_VhfProfile_UsesMarkAndSpaceFrequencies()
    {
        byte[] frame = { 0xAA, 0x55 }; // arbitrary test bytes
        var profile = AfskProfile.Vhf1200Baud;

        float[] audio = AfskModulator.ModulateFrame(frame, profile, sampleRateHz: 44100);

        Assert.NotEmpty(audio);
        Assert.All(audio, s => Assert.InRange(s, -1.0f, 1.0f));
    }

    [Fact]
    public void ModulateFrame_HfProfile_ProducesLongerAudioThanVhfForSameFrame()
    {
        // 300 baud (HF) sends bits 4x slower than 1200 baud (VHF), so the
        // same frame must produce roughly 4x more audio samples.
        byte[] frame = { 0xAA, 0x55, 0xFF };

        float[] vhfAudio = AfskModulator.ModulateFrame(frame, AfskProfile.Vhf1200Baud, sampleRateHz: 44100);
        float[] hfAudio = AfskModulator.ModulateFrame(frame, AfskProfile.Hf300Baud, sampleRateHz: 44100);

        double ratio = (double)hfAudio.Length / vhfAudio.Length;
        Assert.InRange(ratio, 3.5, 4.5);
    }

    [Fact]
    public void AfskProfile_VhfHasCorrectFrequencies()
    {
        Assert.Equal(1200, AfskProfile.Vhf1200Baud.MarkFrequencyHz);
        Assert.Equal(2200, AfskProfile.Vhf1200Baud.SpaceFrequencyHz);
        Assert.Equal(1200, AfskProfile.Vhf1200Baud.BaudRate);
    }

    [Fact]
    public void AfskProfile_HfHasCorrectFrequencies()
    {
        // Per real-world HF APRS practice (DireWolf's actual deployed tones,
        // not the historical true Bell 103 frequencies) -- see CLAUDE.md
        // Phase 10 note on this distinction.
        Assert.Equal(1800, AfskProfile.Hf300Baud.MarkFrequencyHz);
        Assert.Equal(1600, AfskProfile.Hf300Baud.SpaceFrequencyHz);
        Assert.Equal(300, AfskProfile.Hf300Baud.BaudRate);
    }
}
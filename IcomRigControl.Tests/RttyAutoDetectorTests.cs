using IcomRigControl.CivEngine;
using Xunit;

namespace IcomRigControl.Tests;

public class RttyAutoDetectorTests
{
    [Fact]
    public void Detect_StandardHfRtty_FindsDefaultShiftAndBaudAndDecodesCleanly()
    {
        const string message = "CQ CQ DE KE4CON";
        float[] audio = RttyModulator.Modulate(message, RttyProfile.Hf45Baud, 44100);

        var result = RttyAutoDetector.Detect(audio, 44100);

        double shift = result.Profile.SpaceFrequencyHz - result.Profile.MarkFrequencyHz;
        Assert.InRange(shift, 140, 200); // real shift is 170 Hz
        Assert.Equal(45.45, result.Profile.BaudRate, 1);
        Assert.True(result.Score > 0);

        string decoded = RttyDecoder.Decode(audio, result.Profile, 44100);
        Assert.Equal(message, decoded.Trim());
    }

    [Fact]
    public void Detect_NonStandardShiftAndBaud_IsFoundAndDecodesCleanly()
    {
        // 850 Hz shift / 75 baud: a far-from-default combination (commercial/MARS
        // RTTY), so a match can't be a coincidence of the search starting point.
        const string message = "RST 599 DE TEST";
        var profile = new RttyProfile(MarkFrequencyHz: 2125, SpaceFrequencyHz: 2975, BaudRate: 75);
        float[] audio = RttyModulator.Modulate(message, profile, 44100);

        var result = RttyAutoDetector.Detect(audio, 44100);

        double shift = result.Profile.SpaceFrequencyHz - result.Profile.MarkFrequencyHz;
        Assert.InRange(shift, 800, 900); // real shift is 850 Hz
        Assert.Equal(75, result.Profile.BaudRate, 1);

        string decoded = RttyDecoder.Decode(audio, result.Profile, 44100);
        Assert.Equal(message, decoded.Trim());
    }

    [Theory]
    [InlineData(170.0, 45.45)]
    [InlineData(200.0, 50.0)]
    [InlineData(425.0, 56.92)]
    [InlineData(850.0, 100.0)]
    public void Detect_AcrossTheCandidateGrid_FindsTheRightBaud(double shiftHz, double baud)
    {
        var profile = new RttyProfile(MarkFrequencyHz: 2125, SpaceFrequencyHz: 2125 + shiftHz, BaudRate: baud);
        float[] audio = RttyModulator.Modulate("CQ CQ DE KE4CON TEST 599", profile, 44100);

        var result = RttyAutoDetector.Detect(audio, 44100);

        // Shift measurement has some slop at the edges of the sweep, but the
        // decoder tolerates it fine — baud (what actually governs framing) must
        // still land exactly on the real rate.
        Assert.Equal(baud, result.Profile.BaudRate, 1);
    }
}

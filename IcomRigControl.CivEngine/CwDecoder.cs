namespace IcomRigControl.CivEngine;

/// <summary>
/// Convenience one-shot CW decode of a complete audio buffer: runs it through a
/// CwToneDetector (audio -&gt; keyed segments) and a MorseDecoder (segments -&gt; text)
/// and returns the decoded string. The live path (CwDecodeService) drives the same
/// two components incrementally; this static helper is the offline form used by the
/// modulate-&gt;decode round-trip test. See CLAUDE.md CW decode.
/// </summary>
public static class CwDecoder
{
    public static string Decode(float[] audio, double pitchHz = 600, int sampleRateHz = 44100,
                                double initialWpm = 20)
    {
        var detector = new CwToneDetector(pitchHz, sampleRateHz);
        var decoder = new MorseDecoder(initialWpm);
        var sb = new System.Text.StringBuilder();

        foreach (var seg in detector.Feed(audio))
            sb.Append(decoder.Add(seg.On, seg.Seconds));

        if (detector.Flush() is { } last)
            sb.Append(decoder.Add(last.On, last.Seconds));

        sb.Append(decoder.Flush());
        return sb.ToString();
    }
}

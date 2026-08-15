namespace IcomRigControl.CivEngine;

/// <summary>
/// AFSK demodulator — the receive mirror of AfskModulator. Turns audio samples
/// back into the NRZI-decoded bit stream (HDLC flags + stuffed data included),
/// which HdlcDeframer then turns into frames. For each bit period it compares the
/// energy at the mark vs space tone (Goertzel), recovers the tone/level sequence,
/// then NRZI-decodes it.
///
/// Bit timing is fixed slicing from sample 0 — exact for our own modulator's
/// output (the round-trip test) and clean signals. Off-air signals with timing
/// drift would want transition-tracking bit sync; that's a live-hardware refinement.
/// See CLAUDE.md HF APRS receive.
/// </summary>
public static class AfskDemodulator
{
    /// Demodulate float audio (-1..1) to the NRZI-decoded bit stream.
    public static bool[] Demodulate(float[] audio, AfskProfile profile, int sampleRateHz)
    {
        int samplesPerBit = (int)System.Math.Round(sampleRateHz / (double)profile.BaudRate);
        if (samplesPerBit <= 0 || audio.Length < samplesPerBit) return System.Array.Empty<bool>();

        int bitCount = audio.Length / samplesPerBit;
        var levels = new bool[bitCount];
        for (int i = 0; i < bitCount; i++)
        {
            double mark = Goertzel(audio, i * samplesPerBit, samplesPerBit, profile.MarkFrequencyHz, sampleRateHz);
            double space = Goertzel(audio, i * samplesPerBit, samplesPerBit, profile.SpaceFrequencyHz, sampleRateHz);
            levels[i] = mark >= space; // true = mark tone
        }

        // NRZI decode: a level equal to the previous one is a '1', a changed level
        // is a '0'. The modulator starts NRZI at level 'true'.
        var bits = new bool[bitCount];
        bool previous = true;
        for (int i = 0; i < bitCount; i++)
        {
            bits[i] = levels[i] == previous;
            previous = levels[i];
        }
        return bits;
    }

    /// Demodulate 16-bit PCM (e.g. from IAudioCapture) — converts to float first.
    public static bool[] Demodulate(short[] pcm, AfskProfile profile, int sampleRateHz)
    {
        var audio = new float[pcm.Length];
        for (int i = 0; i < pcm.Length; i++) audio[i] = pcm[i] / 32768f;
        return Demodulate(audio, profile, sampleRateHz);
    }

    private static double Goertzel(float[] x, int start, int count, double freq, int sampleRate)
    {
        double w = 2 * System.Math.PI * freq / sampleRate;
        double coeff = 2 * System.Math.Cos(w);
        double s1 = 0, s2 = 0;
        for (int i = 0; i < count; i++)
        {
            double s0 = x[start + i] + coeff * s1 - s2;
            s2 = s1;
            s1 = s0;
        }
        return s1 * s1 + s2 * s2 - coeff * s1 * s2;
    }
}

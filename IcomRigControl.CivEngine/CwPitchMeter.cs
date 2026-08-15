using System;

namespace IcomRigControl.CivEngine;

/// <summary>
/// Measures the actual audio pitch of a received CW tone, so the app can tell how
/// far — and which way — the signal is off from the target CW pitch. That is the
/// basis of "zero beat": when the received tone equals your CW pitch, you are tuned
/// exactly onto the other station's frequency, so your transmission lands right on
/// theirs. See CLAUDE.md CW decode / zero beat.
///
/// It runs a little bank of Goertzel filters across a span around the target pitch,
/// finds the strongest bin, and parabolically interpolates its two neighbors for a
/// sub-bin frequency estimate. Returns null when there is no clear tone (too weak,
/// or the peak isn't distinct from the surrounding noise), so a caller never
/// "zero beats" onto noise.
/// </summary>
public static class CwPitchMeter
{
    /// <param name="audio">Float audio (-1..1) covering a keyed interval (a dit/dah).</param>
    /// <param name="centerHz">Target CW pitch to search around, e.g. 600 Hz.</param>
    /// <param name="spanHz">Half-width of the search, e.g. 300 Hz searches 300..900.</param>
    /// <param name="stepHz">Bin spacing of the filter bank.</param>
    /// <returns>The measured tone frequency in Hz, or null if no clear tone.</returns>
    public static double? MeasureToneHz(float[] audio, int sampleRateHz,
                                        double centerHz = 600, double spanHz = 300, double stepHz = 10)
    {
        if (audio.Length < sampleRateHz / 50) return null; // need ~20 ms of audio

        double lowHz = Math.Max(stepHz, centerHz - spanHz);
        double highHz = centerHz + spanHz;
        int bins = (int)Math.Round((highHz - lowHz) / stepHz) + 1;
        if (bins < 3) return null;

        var mags = new double[bins];
        double sum = 0, peak = 0;
        int peakIndex = 0;
        for (int i = 0; i < bins; i++)
        {
            double f = lowHz + i * stepHz;
            double m = GoertzelPower(audio, 0, audio.Length, f, sampleRateHz);
            mags[i] = m;
            sum += m;
            if (m > peak) { peak = m; peakIndex = i; }
        }

        // Require a distinct peak: clearly above the average energy in the band.
        double mean = sum / bins;
        if (peak <= 0 || peak < 4 * mean) return null;

        double measured = lowHz + peakIndex * stepHz;

        // Parabolic interpolation across the peak and its neighbors for sub-step accuracy.
        if (peakIndex > 0 && peakIndex < bins - 1)
        {
            double a = mags[peakIndex - 1], b = mags[peakIndex], c = mags[peakIndex + 1];
            double denom = a - 2 * b + c;
            if (Math.Abs(denom) > 1e-12)
            {
                double delta = 0.5 * (a - c) / denom; // -0.5..+0.5 of a bin
                measured += delta * stepHz;
            }
        }
        return measured;
    }

    private static double GoertzelPower(float[] x, int start, int count, double freq, int sampleRate)
    {
        double w = 2 * Math.PI * freq / sampleRate;
        double coeff = 2 * Math.Cos(w);
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

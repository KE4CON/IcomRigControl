using System;
using System.Collections.Generic;

namespace IcomRigControl.CivEngine;

/// <summary>
/// Auto-detects the mark/space tones (shift) and baud rate of an RTTY signal from
/// a few seconds of captured audio, instead of requiring the operator to already
/// know them and tune exactly onto 2125/2295 Hz. Two separate, direct
/// measurements, in order:
///
/// 1. SHIFT (and the exact tones): a full-buffer Goertzel sweep of the 300-3000 Hz
///    RTTY passband finds the two strongest tones actually present in the audio —
///    an FSK signal's energy concentrates at exactly two frequencies no matter
///    which bit is being sent at any instant, so this works regardless of bit
///    timing. The lower tone is Mark, the higher is Space (the same convention
///    RttyProfile.Hf45Baud uses: 2125 &lt; 2295). An earlier version of this tried
///    to brute-force candidate shifts with RttyDecoder directly and scored by
///    decoded character count; that rewarded wrong, FASTER bauds (more candidate
///    start-bit attempts per second is just more lucky framing matches on noise,
///    not a real lock) and narrow-window candidate probes couldn't tell 170 Hz
///    shift from 200 Hz apart without smearing bit timing — see git history.
/// 2. BAUD: with the real tones now known, a fine Goertzel sweep across time (at
///    those two exact frequencies) gives a bit-on/bit-off sequence. Every run of
///    consecutive equal bits in it is a whole number of real bit periods (1 for
///    an ordinary bit, more for idle or repeated bits). The candidate baud whose
///    bit period divides every observed run length most evenly (least total
///    squared deviation from an integer count of periods) ranks best — but at a
///    narrow shift (170 Hz), the window wide enough to tell Mark from Space apart
///    is itself close to one whole bit period, which blurs run-length timing
///    enough to confuse two close bauds (45.45 vs 50). So the top few run-length
///    candidates are also actually decoded with RttyDecoder (now that the shift
///    is known) and the cleanest-reading one wins the tie — restricted to just
///    those few CLOSE candidates, not the whole grid, so it isn't the brute-force
///    decode-and-count-characters approach that was tried and rejected for SHIFT
///    (see above): comparing only near neighbors doesn't let a wildly-faster,
///    wrong baud win on lucky noise.
///
/// See CLAUDE.md RTTY decode.
/// </summary>
public static class RttyAutoDetector
{
    public static readonly double[] CandidateBauds = { 45.45, 50, 56.92, 75, 100 };
    public static readonly double[] CandidateShiftsHz = { 170, 200, 425, 850 };

    private const double SweepLoHz = 300;
    private const double SweepHiHz = 3000;
    private const double SweepStepHz = 5;

    public readonly record struct Result(RttyProfile Profile, int Score);

    /// Score is the winning baud's bit-transition count — a rough "was there
    /// really an alternating FSK signal in here" strength indicator. Callers can
    /// treat a very low score (near 0) as "no clear signal".
    public static Result Detect(float[] samples, int sampleRateHz = 44100)
    {
        var (markHz, spaceHz) = FindMarkAndSpace(samples, sampleRateHz);
        double shift = spaceHz - markHz;

        // Window sized from the measured shift, not a fixed guess: a Goertzel
        // bin needs to be noticeably narrower than the shift to tell Mark from
        // Space, so a small shift (170 Hz) gets a wider time window (coarser bit
        // timing but still resolves standard bauds) and a large shift (850 Hz)
        // gets a narrow one (fine timing).
        int window = Math.Clamp((int)(sampleRateHz / Math.Max(shift / 3.0, 20)), 32, sampleRateHz / 10);
        // Decoupled from `window`: the window has to stay wide enough to tell Mark
        // from Space apart (frequency resolution), but run-length timing wants a
        // MUCH finer step than that, so it doesn't lose precision between two
        // close candidate bauds (e.g. 45.45 vs 50) at a wide, small-shift window.
        int step = Math.Max(8, sampleRateHz / 800);

        bool[] bits = BitSequence(samples, sampleRateHz, markHz, spaceHz, step, window);
        double baud = DetectBaud(bits, samples, markHz, spaceHz, sampleRateHz, step);

        return new Result(new RttyProfile(markHz, spaceHz, baud), CountTransitions(bits));
    }

    // Finds the two strongest tones in the RTTY passband over the WHOLE buffer.
    // Both the mark and space tone are present somewhere in a real FSK signal
    // regardless of bit timing, so a single full-length sweep (no windowing)
    // finds them without needing to know the baud first. Excludes a band around
    // the first peak before searching for the second, so both don't land in the
    // same spectral lobe.
    private static (double markHz, double spaceHz) FindMarkAndSpace(float[] samples, int sampleRateHz)
    {
        int bins = (int)((SweepHiHz - SweepLoHz) / SweepStepHz) + 1;
        var freqs = new double[bins];
        var energy = new double[bins];
        for (int i = 0; i < bins; i++)
        {
            freqs[i] = SweepLoHz + i * SweepStepHz;
            energy[i] = Goertzel(samples, 0, samples.Length, freqs[i], sampleRateHz);
        }

        int peak1 = ArgMax(energy, 0, bins);
        // Clear a window around the first peak at least as wide as the smallest
        // real shift, so the second search can't just find the same tone again.
        int exclude = (int)(85 / SweepStepHz) + 1;
        var energy2 = (double[])energy.Clone();
        for (int i = Math.Max(0, peak1 - exclude); i <= Math.Min(bins - 1, peak1 + exclude); i++)
            energy2[i] = double.MinValue;
        int peak2 = ArgMax(energy2, 0, bins);

        double f1 = freqs[peak1], f2 = freqs[peak2];
        return f1 <= f2 ? (f1, f2) : (f2, f1);
    }

    private static int ArgMax(double[] values, int from, int to)
    {
        int best = from;
        for (int i = from + 1; i < to; i++)
            if (values[i] > values[best]) best = i;
        return best;
    }

    private static double DetectBaud(bool[] bits, float[] samples, double markHz, double spaceHz, int sampleRateHz, int step)
    {
        var runLengths = RunLengths(bits);

        var ranked = new List<(double baud, double error)>();
        foreach (double baud in CandidateBauds)
        {
            double bitPeriodSteps = sampleRateHz / baud / step;
            double error = 0;
            foreach (int run in runLengths)
            {
                // A run shorter than one candidate bit period is still measuring
                // SOME real bit (never zero periods) — but don't let a guess with
                // too-large a period get a free pass by flooring to exactly 1; the
                // deviation from that floor must still count against it.
                double periods = run / bitPeriodSteps;
                double nearest = Math.Max(Math.Round(periods), 1);
                double deviation = periods - nearest;
                error += deviation * deviation;
            }
            ranked.Add((baud, error));
        }
        ranked.Sort((a, b) => a.error.CompareTo(b.error));

        // Only the closest few by run-length fit get a real decode attempt —
        // comparing nearby candidates this way is reliable; comparing the WHOLE
        // grid this way is exactly the brute-force approach that was tried and
        // rejected (a far-too-fast wrong baud decodes more "characters" purely
        // from having more framing attempts per second, not from being right).
        double bestBaud = ranked[0].baud;
        int bestScore = DecodeScore(samples, markHz, spaceHz, bestBaud, sampleRateHz);
        for (int i = 1; i < Math.Min(3, ranked.Count); i++)
        {
            int score = DecodeScore(samples, markHz, spaceHz, ranked[i].baud, sampleRateHz);
            if (score > bestScore)
            {
                bestScore = score;
                bestBaud = ranked[i].baud;
            }
        }
        return bestBaud;
    }

    private static int DecodeScore(float[] samples, double markHz, double spaceHz, double baud, int sampleRateHz)
    {
        string decoded = RttyDecoder.Decode(samples, new RttyProfile(markHz, spaceHz, baud), sampleRateHz);
        int score = 0;
        foreach (char c in decoded)
            if (!char.IsControl(c)) score++;
        return score;
    }

    private static List<int> RunLengths(bool[] bits)
    {
        var runs = new List<int>();
        if (bits.Length == 0) return runs;

        int run = 1;
        for (int i = 1; i < bits.Length; i++)
        {
            if (bits[i] == bits[i - 1]) run++;
            else { runs.Add(run); run = 1; }
        }
        runs.Add(run);
        return runs;
    }

    private static int CountTransitions(bool[] bits)
    {
        int count = 0;
        for (int i = 1; i < bits.Length; i++)
            if (bits[i] != bits[i - 1]) count++;
        return count;
    }

    // One bit value per `step`-sized time slot, each read from a `window`-wide
    // Goertzel comparison of mark vs space centered on that slot.
    private static bool[] BitSequence(float[] samples, int sampleRateHz, double markHz, double spaceHz, int step, int window)
    {
        var bits = new List<bool>();
        for (int start = 0; start + window <= samples.Length; start += step)
        {
            double mark = Goertzel(samples, start, window, markHz, sampleRateHz);
            double space = Goertzel(samples, start, window, spaceHz, sampleRateHz);
            bits.Add(mark >= space);
        }
        return bits.ToArray();
    }

    private static double Goertzel(float[] x, int start, int count, double freq, int sampleRate)
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

using System;
using System.Collections.Generic;
using System.Text;

namespace IcomRigControl.CivEngine;

/// <summary>
/// Streaming RTTY decoder: turns FSK audio into text. For each hypothesised
/// character it locates a start bit (a drop from the idle mark tone to space),
/// samples the five data bits at their centers by comparing mark-vs-space energy
/// (Goertzel — the same tone-discrimination the APRS demodulator uses), verifies a
/// stop bit (mark), then maps the 5 bits through BaudotCode using the running
/// LETTERS/FIGURES shift state.
///
/// Async framing (start/stop bits) means there is no continuous clock to recover —
/// each character re-syncs on its own start bit — so this copes with idle gaps
/// between characters. "Reverse" swaps mark/space for the sideband/USB-vs-LSB case.
/// "Unshift on space" (USOS), on by default, drops back to LETTERS after a space,
/// which is how real decoders recover from a missed FIGS/LTRS shift. Stateful and
/// streaming: feed it audio as it arrives; it keeps the unprocessed tail between
/// calls. See CLAUDE.md RTTY decode.
/// </summary>
public sealed class RttyDecoder
{
    private readonly double _markHz;
    private readonly double _spaceHz;
    private readonly int _sampleRate;
    private readonly double _spb;
    private readonly bool _usos;

    private readonly List<float> _buffer = new();
    private bool _figures;
    private bool _armed; // true once idle mark seen, so the next space is a real start bit

    public RttyDecoder(RttyProfile? profile = null, int sampleRateHz = 44100,
                       bool reverse = false, bool unshiftOnSpace = true)
    {
        var p = profile ?? RttyProfile.Hf45Baud;
        // "Reverse" simply swaps which tone counts as mark.
        _markHz = reverse ? p.SpaceFrequencyHz : p.MarkFrequencyHz;
        _spaceHz = reverse ? p.MarkFrequencyHz : p.SpaceFrequencyHz;
        _sampleRate = sampleRateHz;
        _spb = sampleRateHz / p.BaudRate;
        _usos = unshiftOnSpace;
    }

    /// Feeds a chunk of float audio (-1..1). Returns any newly decoded text.
    public string Feed(float[] samples)
    {
        _buffer.AddRange(samples);
        var sb = new StringBuilder();

        int spb = (int)Math.Round(_spb);
        int step = Math.Max(1, spb / 16);
        // Samples needed ahead of `p` to sample through the stop bit (see geometry below).
        int need = (int)Math.Ceiling(8 * _spb);

        int p = 0;
        while (p + need <= _buffer.Count)
        {
            // IsMark(c) integrates the full bit centered on c, so as `p` advances the
            // reading tips from mark to space when the window is half over the start
            // edge — i.e. the first SPACE reading happens with the window centered on
            // the true edge. So the start edge is at p + 0.5 bit, and bit centers are
            // measured from there.
            bool bitIsMark = IsMark(p + _spb * 0.5);

            // Re-sync each character on a real start bit: only after seeing idle/stop
            // MARK ("armed") does a following SPACE count as a start edge. Without this
            // the decoder free-runs and drifts by the fractional stop bit.
            if (!_armed)
            {
                if (bitIsMark) _armed = true;
                p += step;
                continue;
            }
            if (bitIsMark) { p += step; continue; } // armed, still idle mark — keep waiting

            double edge = p + 0.5 * _spb; // true leading edge of the start bit

            // Sample the five data bits at their centers (mark = 1); require stop = mark.
            var bits = new char[5];
            for (int n = 0; n < 5; n++)
                bits[n] = IsMark(edge + _spb * (1.5 + n)) ? '1' : '0';

            _armed = false; // must re-see idle mark before accepting the next start
            if (!IsMark(edge + _spb * 6.5))
            {
                p += step; // stop bit not mark: framing error, resync from here
                continue;
            }

            // Data bits were sent LSB-first; BaudotCode keys are MSB-first.
            string code = new string(new[] { bits[4], bits[3], bits[2], bits[1], bits[0] });
            sb.Append(Interpret(code));

            // Jump into the stop bit (mark); the loop re-arms on it, then finds the next
            // start edge — so 1, 1.5 or 2 stop bits all resync cleanly.
            p = (int)Math.Round(edge + _spb * 6.0);
        }

        if (p > 0) _buffer.RemoveRange(0, p);
        return sb.ToString();
    }

    private string Interpret(string code)
    {
        switch (code)
        {
            case BaudotCode.Ltrs: _figures = false; return "";
            case BaudotCode.Figs: _figures = true; return "";
            case BaudotCode.Null: return "";
            case BaudotCode.Cr: return "";      // fold CR/LF into a single newline on LF
            case BaudotCode.Lf: return "\n";
            case BaudotCode.Space:
                if (_usos) _figures = false;
                return " ";
            default:
                char? c = BaudotCode.Decode(code, _figures);
                return c is { } ch && !char.IsControl(ch) ? ch.ToString() : "";
        }
    }

    // Compares mark-vs-space energy over one bit centered on `center` (buffer-relative).
    // A full-bit window spans enough cycles to separate the 170 Hz-apart tones cleanly.
    private bool IsMark(double center)
    {
        int half = (int)Math.Round(_spb / 2);
        int start = (int)Math.Round(center) - half;
        int count = 2 * half;
        if (start < 0) { count += start; start = 0; }
        if (start + count > _buffer.Count) count = _buffer.Count - start;
        if (count <= 0) return true; // default idle = mark

        double mark = Goertzel(_buffer, start, count, _markHz, _sampleRate);
        double space = Goertzel(_buffer, start, count, _spaceHz, _sampleRate);
        return mark >= space;
    }

    private static double Goertzel(List<float> x, int start, int count, double freq, int sampleRate)
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

    /// One-shot decode of a complete buffer — the offline form used by the
    /// modulate-&gt;decode round-trip test.
    public static string Decode(float[] audio, RttyProfile? profile = null, int sampleRateHz = 44100,
                                bool reverse = false, bool unshiftOnSpace = true)
    {
        var d = new RttyDecoder(profile, sampleRateHz, reverse, unshiftOnSpace);
        return d.Feed(audio);
    }
}

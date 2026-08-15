using System.Text;

namespace IcomRigControl.CivEngine;

/// <summary>
/// Streaming Morse decoder: fed a sequence of tone ON/OFF segments (each with a
/// duration in seconds), it recovers characters. It tracks the sender's speed
/// adaptively — the "dot" length is a running estimate, so it follows a fist that
/// speeds up or slows down — and classifies each keyed element as a dit or dah,
/// and each gap as an element gap, character gap, or word gap.
///
/// Timing rules (Morse standard, in "units" where a dit = 1 unit): dit = 1,
/// dah = 3, gap between elements of a character = 1, gap between characters = 3,
/// gap between words = 7. Classification thresholds sit at the midpoints (2 units
/// dit/dah, 2 units element/character gap, 5 units character/word gap).
///
/// Pure timing logic — no audio, no DSP. That makes it exhaustively unit-testable:
/// feed it a hand-built timing sequence and assert the decoded text. See CLAUDE.md
/// CW decode. Note: like every CW reader, the first character or two may be garbled
/// until the speed estimate locks; badly-sent ("poor fist") CW degrades gracefully.
/// </summary>
public sealed class MorseDecoder
{
    private double _dotSec;
    private readonly StringBuilder _current = new();

    // How fast the dot-length estimate chases the observed timing. Low enough to
    // ride out one bad element, high enough to track a real speed change.
    private const double Ema = 0.15;

    /// <param name="initialWpm">Speed the decoder assumes until it has locked onto
    /// the sender — 20 WPM is a typical starting point.</param>
    public MorseDecoder(double initialWpm = 20)
    {
        _dotSec = 1.2 / initialWpm; // PARIS standard: dit seconds = 1.2 / WPM
    }

    /// The current estimated sender speed in words per minute.
    public double EstimatedWpm => _dotSec > 0 ? 1.2 / _dotSec : 0;

    /// Feeds one tone segment. Returns any text completed by this segment (a
    /// character when a character gap ends the current symbol, plus a space on a
    /// word gap) — usually "" for the many segments that only extend the symbol.
    public string Add(bool toneOn, double seconds)
    {
        if (toneOn)
        {
            // Ignore a blip far shorter than a dit — that's a noise spike, not a key-down.
            if (seconds < 0.35 * _dotSec) return "";

            bool isDah = seconds >= 2 * _dotSec;
            _current.Append(isDah ? '-' : '.');

            // Re-estimate the dit length from this element (a dah is ~3 dits).
            double observedDit = isDah ? seconds / 3.0 : seconds;
            _dotSec += Ema * (observedDit - _dotSec);
            return "";
        }

        // Tone off: a gap. Anything under ~2 dits is a within-character element gap.
        if (seconds < 2 * _dotSec) return "";

        // 2+ dits ends the current character; 5+ dits also inserts a word space.
        string text = FinalizeCharacter();
        if (text.Length > 0 && seconds >= 5 * _dotSec) text += " ";
        return text;
    }

    /// Emits whatever character is currently accumulated (call at end of stream, or
    /// after a long trailing silence). Returns "" if nothing is pending.
    public string Flush() => FinalizeCharacter();

    private string FinalizeCharacter()
    {
        if (_current.Length == 0) return "";
        char? c = MorseCode.Decode(_current.ToString());
        _current.Clear();
        return c is { } ch ? ch.ToString() : "";
    }
}

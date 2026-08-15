using System.Collections.Generic;

namespace IcomRigControl.CivEngine;

/// <summary>
/// The Baudot / ITA2 5-bit teleprinter code used by HF RTTY. Five data bits only
/// give 32 codes, far fewer than the ~60 characters needed, so the code has two
/// "shift" states: LETTERS (LTRS) and FIGURES (FIGS). Special codes switch between
/// them, and the same 5 bits mean a letter in one state and a digit/punctuation in
/// the other. A few codes (space, carriage return, line feed, the two shifts) are
/// common to both states.
///
/// Bit convention here: patterns are written most-significant-bit first as a 5-char
/// "01"/"1" string where '1' = mark. On the air the least-significant bit is sent
/// first, framed by a start bit (space) and stop bit (mark) — that framing lives in
/// RttyModulator / RttyDecoder, not here. This class is a pure table plus an encoder
/// that inserts the right shift codes. See CLAUDE.md RTTY decode.
/// </summary>
public static class BaudotCode
{
    public const string Ltrs = "11111";
    public const string Figs = "11011";
    public const string Space = "00100";
    public const string Cr = "00010";
    public const string Lf = "01000";
    public const string Null = "00000";

    // (5-bit pattern, letter, figure). Figures follow the common US/amateur layout
    // (QWERTY top row = 1234567890). BELL is non-printing and mapped to '\a'.
    private static readonly (string Bits, char Letter, char Figure)[] Rows =
    {
        ("00011", 'A', '-'), ("11001", 'B', '?'), ("01110", 'C', ':'), ("01001", 'D', '$'),
        ("00001", 'E', '3'), ("01101", 'F', '!'), ("11010", 'G', '&'), ("10100", 'H', '#'),
        ("00110", 'I', '8'), ("01011", 'J', '\a'), ("01111", 'K', '('), ("10010", 'L', ')'),
        ("11100", 'M', '.'), ("01100", 'N', ','), ("11000", 'O', '9'), ("10110", 'P', '0'),
        ("10111", 'Q', '1'), ("01010", 'R', '4'), ("00101", 'S', '\''), ("10000", 'T', '5'),
        ("00111", 'U', '7'), ("11110", 'V', ';'), ("10011", 'W', '2'), ("11101", 'X', '/'),
        ("10101", 'Y', '6'), ("10001", 'Z', '"'),
    };

    private static readonly Dictionary<string, char> LetterMap = new();
    private static readonly Dictionary<string, char> FigureMap = new();
    private static readonly Dictionary<char, string> LetterReverse = new();
    private static readonly Dictionary<char, string> FigureReverse = new();

    static BaudotCode()
    {
        foreach (var (bits, letter, figure) in Rows)
        {
            LetterMap[bits] = letter;
            FigureMap[bits] = figure;
            LetterReverse.TryAdd(letter, bits);
            FigureReverse.TryAdd(figure, bits);
        }
    }

    /// Looks up a data code in the given shift state. Returns null for the shift and
    /// control codes (the caller handles those) or an unmapped pattern.
    public static char? Decode(string bits, bool figures) =>
        figures
            ? (FigureMap.TryGetValue(bits, out char f) ? f : null)
            : (LetterMap.TryGetValue(bits, out char l) ? l : null);

    /// Encodes text into the sequence of 5-bit codes to send, inserting LTRS/FIGS
    /// shift codes as the state changes and mapping spaces/newlines to their codes.
    /// Characters with no Baudot representation are skipped. Used by RttyModulator.
    public static List<string> Encode(string text)
    {
        var codes = new List<string>();
        bool figures = false;
        foreach (char raw in text)
        {
            char c = char.ToUpperInvariant(raw);
            if (c == ' ') { codes.Add(Space); continue; }
            if (c == '\n') { codes.Add(Cr); codes.Add(Lf); continue; }
            if (c == '\r') continue;

            if (LetterReverse.TryGetValue(c, out string? lb))
            {
                if (figures) { codes.Add(Ltrs); figures = false; }
                codes.Add(lb);
            }
            else if (FigureReverse.TryGetValue(c, out string? fb))
            {
                if (!figures) { codes.Add(Figs); figures = true; }
                codes.Add(fb);
            }
            // else: no Baudot code for this character — skip it.
        }
        return codes;
    }
}

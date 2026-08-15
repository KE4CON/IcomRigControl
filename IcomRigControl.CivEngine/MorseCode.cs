using System.Collections.Generic;

namespace IcomRigControl.CivEngine;

/// <summary>
/// The International Morse Code table: maps between characters and their
/// dot/dash patterns ('.' = dit, '-' = dah). Used by MorseDecoder (pattern -&gt;
/// character) and CwModulator (character -&gt; pattern). Covers letters, digits,
/// and the common punctuation/prosigns that map to standard ASCII. This is a
/// pure lookup table — no timing, no audio. See CLAUDE.md CW decode.
/// </summary>
public static class MorseCode
{
    // Pattern -> character. Prosigns that share a pattern with a punctuation mark
    // (e.g. "-...-" is both BT and "=") are listed once, as the punctuation char;
    // operators read them by context.
    private static readonly Dictionary<string, char> PatternToChar = new()
    {
        // Letters
        [".-"] = 'A', ["-..."] = 'B', ["-.-."] = 'C', ["-.."] = 'D', ["."] = 'E',
        ["..-."] = 'F', ["--."] = 'G', ["...."] = 'H', [".."] = 'I', [".---"] = 'J',
        ["-.-"] = 'K', [".-.."] = 'L', ["--"] = 'M', ["-."] = 'N', ["---"] = 'O',
        [".--."] = 'P', ["--.-"] = 'Q', [".-."] = 'R', ["..."] = 'S', ["-"] = 'T',
        ["..-"] = 'U', ["...-"] = 'V', [".--"] = 'W', ["-..-"] = 'X', ["-.--"] = 'Y',
        ["--.."] = 'Z',
        // Digits
        ["-----"] = '0', [".----"] = '1', ["..---"] = '2', ["...--"] = '3', ["....-"] = '4',
        ["....."] = '5', ["-...."] = '6', ["--..."] = '7', ["---.."] = '8', ["----."] = '9',
        // Punctuation / prosigns
        [".-.-.-"] = '.', ["--..--"] = ',', ["..--.."] = '?', [".----."] = '\'',
        ["-.-.--"] = '!', ["-..-."] = '/', ["-.--."] = '(', ["-.--.-"] = ')',
        [".-..."] = '&', ["---..."] = ':', ["-.-.-."] = ';', ["-...-"] = '=',
        [".-.-."] = '+', ["-....-"] = '-', ["..--.-"] = '_', [".-..-."] = '"',
        ["...-..-"] = '$', [".--.-."] = '@',
    };

    private static readonly Dictionary<char, string> CharToPattern = BuildReverse();

    private static Dictionary<char, string> BuildReverse()
    {
        var map = new Dictionary<char, string>();
        foreach (var kv in PatternToChar)
            map.TryAdd(kv.Value, kv.Key); // first pattern wins for any shared char
        return map;
    }

    /// Decodes a dot/dash pattern to its character, or null if unknown (garble).
    public static char? Decode(string pattern) =>
        PatternToChar.TryGetValue(pattern, out char c) ? c : null;

    /// Encodes a character (case-insensitive) to its dot/dash pattern, or null if
    /// the character has no Morse representation.
    public static string? Encode(char c) =>
        CharToPattern.TryGetValue(char.ToUpperInvariant(c), out string? p) ? p : null;
}

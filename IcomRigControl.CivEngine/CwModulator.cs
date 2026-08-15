using System;
using System.Collections.Generic;

namespace IcomRigControl.CivEngine;

/// <summary>
/// Generates on/off-keyed (OOK) audio for a Morse message: a single tone switched
/// on for dits and dahs with the standard inter-element, inter-character and
/// inter-word gaps. This is the transmit mirror of the CW receive chain
/// (CwToneDetector + MorseDecoder), and its main job is to make the decoder
/// testable without a radio — modulate a known string, feed it back through the
/// detector and decoder, and assert the text round-trips. See CLAUDE.md CW decode.
/// </summary>
public static class CwModulator
{
    /// <param name="text">Message to key. Characters with no Morse mapping are skipped.</param>
    /// <param name="wpm">Speed in words per minute (PARIS standard).</param>
    /// <param name="pitchHz">Tone frequency (the CW pitch), e.g. 600 Hz.</param>
    /// <param name="sampleRateHz">Output sample rate.</param>
    /// <param name="amplitude">Peak tone amplitude (0..1).</param>
    public static float[] Modulate(string text, double wpm = 20, double pitchHz = 600,
                                   int sampleRateHz = 44100, double amplitude = 0.6)
    {
        double ditSec = 1.2 / wpm;
        int ditSamples = (int)Math.Round(ditSec * sampleRateHz);
        var samples = new List<float>();

        bool firstCharOfWord = true;
        bool anyCharYet = false;

        foreach (char raw in text)
        {
            if (raw == ' ')
            {
                if (anyCharYet) AppendSilence(samples, 7 * ditSamples); // word gap
                firstCharOfWord = true;
                continue;
            }

            string? pattern = MorseCode.Encode(raw);
            if (pattern is null) continue;

            if (anyCharYet && !firstCharOfWord)
                AppendSilence(samples, 3 * ditSamples); // character gap

            for (int i = 0; i < pattern.Length; i++)
            {
                if (i > 0) AppendSilence(samples, ditSamples); // element gap
                AppendTone(samples, pattern[i] == '-' ? 3 * ditSamples : ditSamples,
                           pitchHz, sampleRateHz, amplitude);
            }

            anyCharYet = true;
            firstCharOfWord = false;
        }

        return samples.ToArray();
    }

    private static void AppendTone(List<float> buf, int count, double freq, int sampleRate, double amp)
    {
        double w = 2 * Math.PI * freq / sampleRate;
        int start = buf.Count;
        for (int i = 0; i < count; i++)
            buf.Add((float)(amp * Math.Sin(w * (start + i))));
    }

    private static void AppendSilence(List<float> buf, int count)
    {
        for (int i = 0; i < count; i++) buf.Add(0f);
    }
}

using System;
using System.Collections.Generic;

namespace IcomRigControl.CivEngine;

/// <summary>
/// Generates RTTY (Baudot FSK) audio for a text string: each character becomes a
/// start bit (space tone), five data bits sent least-significant-bit first (mark
/// tone for a 1, space for a 0), and stop bits (mark tone), with LTRS/FIGS shift
/// codes inserted as needed by BaudotCode. The two tones are produced with a single
/// continuous-phase oscillator so there are no clicks at bit boundaries — real FSK.
///
/// Like CwModulator, its main job is to make the decoder testable without a radio:
/// modulate a known string, feed it back through RttyDecoder, and assert the text
/// round-trips. See CLAUDE.md RTTY decode.
/// </summary>
public static class RttyModulator
{
    public static float[] Modulate(string text, RttyProfile? profile = null, int sampleRateHz = 44100,
                                   double stopBits = 1.5, double amplitude = 0.6, int leadIdleBits = 8)
    {
        var p = profile ?? RttyProfile.Hf45Baud;
        int spb = (int)Math.Round(sampleRateHz / p.BaudRate);
        var samples = new List<float>();
        double phase = 0;

        void Tone(double freq, int count)
        {
            double w = 2 * Math.PI * freq / sampleRateHz;
            for (int i = 0; i < count; i++)
            {
                samples.Add((float)(amplitude * Math.Sin(phase)));
                phase += w;
            }
        }

        void Mark(double bits) => Tone(p.MarkFrequencyHz, (int)Math.Round(bits * spb));
        void SpaceTone(double bits) => Tone(p.SpaceFrequencyHz, (int)Math.Round(bits * spb));

        Mark(leadIdleBits); // idle line is mark

        foreach (string code in BaudotCode.Encode(text))
        {
            SpaceTone(1);                 // start bit
            for (int n = 0; n < 5; n++)   // data bits, LSB (rightmost char) first
            {
                bool markBit = code[4 - n] == '1';
                if (markBit) Mark(1); else SpaceTone(1);
            }
            Mark(stopBits);               // stop bit(s)
        }

        Mark(leadIdleBits); // trailing idle
        return samples.ToArray();
    }
}

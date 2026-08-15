namespace IcomRigControl.CivEngine;

/// <summary>
/// Mark/space audio tones and baud rate for an RTTY (radioteletype) modem. HF
/// amateur RTTY is a narrow FSK signal: two tones a fixed "shift" apart, keyed at
/// 45.45 baud. The classic tones are 2125 Hz (mark) and 2295 Hz (space) — a 170 Hz
/// shift — which is what most decoders and the RTTY tuning indicators on radios
/// expect. Baud is fractional (exactly 1000/22), so this is its own type rather
/// than reusing the integer-baud AfskProfile. See CLAUDE.md RTTY decode.
/// </summary>
public record RttyProfile(double MarkFrequencyHz, double SpaceFrequencyHz, double BaudRate)
{
    /// Standard HF amateur RTTY: 170 Hz shift, 45.45 baud (60 WPM).
    public static readonly RttyProfile Hf45Baud = new(MarkFrequencyHz: 2125, SpaceFrequencyHz: 2295, BaudRate: 45.45);
}

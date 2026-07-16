namespace IcomRigControl.CivEngine;

/// <summary>
/// Mark/space frequencies and baud rate for an AFSK modem profile. Two
/// well-known profiles are provided: VHF (Bell 202-style, 1200 baud, used
/// on 2m/70cm APRS) and HF (the real-world tones actually used by HF APRS
/// software like DireWolf — 1600/1800 Hz at 300 baud — which differ from
/// the historical, literal Bell 103 telephone-modem frequencies (1070/1270
/// and 2025/2225 Hz). See CLAUDE.md Phase 10 for the research behind this
/// distinction — using true Bell 103 tones would not be decodable by real
/// HF APRS listening stations/software.
/// </summary>
public record AfskProfile(int MarkFrequencyHz, int SpaceFrequencyHz, int BaudRate)
{
    public static readonly AfskProfile Vhf1200Baud = new(MarkFrequencyHz: 1200, SpaceFrequencyHz: 2200, BaudRate: 1200);
    public static readonly AfskProfile Hf300Baud = new(MarkFrequencyHz: 1800, SpaceFrequencyHz: 1600, BaudRate: 300);
}
namespace IcomRigControl.CivEngine;

/// <summary>
/// Extracts AX.25 frames from a raw NRZI-decoded bit stream (the output of
/// AfskDemodulator). Splits on HDLC flags (01111110), removes bit-stuffing
/// (a 0 inserted after five 1s), packs to bytes (LSB-first), and keeps only
/// frames whose FCS checks out — returning each frame WITHOUT its 2 FCS bytes.
/// The receive mirror of AfskModulator.ModulateAx25Frame. See CLAUDE.md HF APRS.
/// </summary>
public static class HdlcDeframer
{
    // The HDLC flag 0x7E as LSB-first bits (same order AfskModulator emits).
    private static readonly bool[] Flag = { false, true, true, true, true, true, true, false };

    public static List<byte[]> ExtractFrames(bool[] bits)
    {
        var frames = new List<byte[]>();

        // Bit-stuffing guarantees the flag pattern can't occur inside data, so
        // every match is a real frame boundary/preamble.
        var flagPositions = new List<int>();
        for (int i = 0; i + Flag.Length <= bits.Length; i++)
            if (MatchAt(bits, i)) flagPositions.Add(i);

        for (int f = 0; f + 1 < flagPositions.Count; f++)
        {
            int start = flagPositions[f] + Flag.Length;
            int end = flagPositions[f + 1];
            if (end <= start) continue; // adjacent flags (preamble) — no data between

            var region = new bool[end - start];
            System.Array.Copy(bits, start, region, 0, region.Length);

            var bytes = BitsToBytes(Destuff(region));
            if (bytes.Length >= 3 && Ax25Fcs.Check(bytes))
                frames.Add(bytes[..^2]); // strip the 2 FCS bytes
        }

        return frames;
    }

    private static bool MatchAt(bool[] bits, int pos)
    {
        for (int i = 0; i < Flag.Length; i++)
            if (bits[pos + i] != Flag[i]) return false;
        return true;
    }

    private static bool[] Destuff(bool[] bits)
    {
        var output = new List<bool>(bits.Length);
        int ones = 0;
        for (int i = 0; i < bits.Length; i++)
        {
            bool b = bits[i];
            if (ones == 5)
            {
                ones = 0;
                if (!b) continue; // drop the stuffed 0
            }
            output.Add(b);
            ones = b ? ones + 1 : 0;
        }
        return output.ToArray();
    }

    private static byte[] BitsToBytes(bool[] bits)
    {
        int n = bits.Length / 8; // whole bytes only; trailing partial bits ignored
        var bytes = new byte[n];
        for (int i = 0; i < n; i++)
            for (int b = 0; b < 8; b++)
                if (bits[i * 8 + b]) bytes[i] |= (byte)(1 << b); // LSB-first
        return bytes;
    }
}

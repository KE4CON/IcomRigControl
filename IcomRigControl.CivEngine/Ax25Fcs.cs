namespace IcomRigControl.CivEngine;

/// <summary>
/// AX.25 Frame Check Sequence — CRC-16/X-25 (polynomial 0x1021 reflected =
/// 0x8408, init 0xFFFF, reflected in/out, final XOR 0xFFFF). The 2-byte FCS is
/// appended to an AX.25 frame (low byte first) inside the HDLC flags, and
/// verified on receive. Without a valid FCS no real APRS decoder accepts the
/// frame — which is why the original beacon (no FCS, no flags) would not decode.
/// </summary>
public static class Ax25Fcs
{
    /// The CRC value over the given bytes.
    public static ushort Compute(ReadOnlySpan<byte> data)
    {
        ushort crc = 0xFFFF;
        foreach (byte b in data)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
                crc = (ushort)((crc & 1) != 0 ? (crc >> 1) ^ 0x8408 : crc >> 1);
        }
        return (ushort)(crc ^ 0xFFFF);
    }

    /// The FCS as appended to a frame on air: low byte first, then high byte.
    public static byte[] ComputeBytes(ReadOnlySpan<byte> data)
    {
        ushort fcs = Compute(data);
        return new[] { (byte)(fcs & 0xFF), (byte)(fcs >> 8) };
    }

    /// True if the last 2 bytes of frameWithFcs are a valid FCS over the rest.
    public static bool Check(ReadOnlySpan<byte> frameWithFcs)
    {
        if (frameWithFcs.Length < 3) return false;
        var frame = frameWithFcs[..^2];
        ushort received = (ushort)(frameWithFcs[^2] | (frameWithFcs[^1] << 8));
        return Compute(frame) == received;
    }
}

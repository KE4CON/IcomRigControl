using System.Text;

namespace IcomRigControl.CivEngine;

/// <summary>
/// Builds AX.25 UI (Unnumbered Information) frames for APRS packets, per the
/// AX.25 Link Access Protocol spec and TAPR APRS 1.0.1 spec. This is the
/// framing layer only — the actual AFSK audio modulation to key the radio
/// is a separate, later piece of Phase 10. See CLAUDE.md Phase 10.
/// </summary>
public static class Ax25FrameBuilder
{
    /// Encodes a single AX.25 address field (7 bytes): the callsign
    /// (space-padded to 6 chars, uppercased, each byte left-shifted 1 bit),
    /// followed by an SSID byte encoding the SSID (0-15), a C/R bit (set to
    /// 1 here, appropriate for a command frame from the source), and the
    /// HDLC address-extension bit (1 only on the last address in the chain).
    public static byte[] EncodeAddress(string callsign, int ssid, bool isLastAddress)
    {
        if (string.IsNullOrEmpty(callsign) || callsign.Length > 6)
        {
            throw new ArgumentException("Callsign must be 1-6 characters.", nameof(callsign));
        }

        if (ssid is < 0 or > 15)
        {
            throw new ArgumentOutOfRangeException(nameof(ssid), "SSID must be 0-15.");
        }

        var result = new byte[7];
        string padded = callsign.ToUpperInvariant().PadRight(6);

        for (int i = 0; i < 6; i++)
        {
            result[i] = (byte)(padded[i] << 1);
        }

        // SSID byte: bit 7 = C/R bit (1), bits 6-5 reserved (set to 1 per
        // convention), bits 4-1 = SSID, bit 0 = extension bit.
        byte ssidByte = (byte)(0b1110_0000 | (ssid << 1));
        if (isLastAddress)
        {
            ssidByte |= 0x01;
        }

        result[6] = ssidByte;
        return result;
    }

    /// Builds a complete AX.25 UI frame for a single-hop APRS packet (no
    /// digipeater path): destination address, source address, Control byte
    /// (0x03 for UI frame), PID byte (0xF0 for no layer 3 protocol), and the
    /// raw APRS info field as plain ASCII bytes.
    public static byte[] BuildUiFrame(
        string sourceCallsign, int sourceSsid,
        string destinationCallsign, int destinationSsid,
        string infoField)
    {
        var destAddress = EncodeAddress(destinationCallsign, destinationSsid, isLastAddress: false);
        var sourceAddress = EncodeAddress(sourceCallsign, sourceSsid, isLastAddress: true);

        var frame = new List<byte>();
        frame.AddRange(destAddress);
        frame.AddRange(sourceAddress);
        frame.Add(0x03); // Control: UI frame
        frame.Add(0xF0); // PID: no layer 3 protocol
        frame.AddRange(Encoding.ASCII.GetBytes(infoField));

        return frame.ToArray();
    }
}
using System.Text;

namespace IcomRigControl.CivEngine;

/// <summary>
/// A decoded AX.25 UI frame: the source/destination callsigns + SSIDs, and the
/// APRS information field. The digipeater path (if any) is captured raw.
/// </summary>
public record Ax25Frame(
    string Source, int SourceSsid,
    string Destination, int DestinationSsid,
    IReadOnlyList<string> Path,
    string InfoField);

/// <summary>
/// Decodes AX.25 UI frame bytes (from HdlcDeframer) into addresses + info — the
/// receive mirror of Ax25FrameBuilder. Handles the destination, source, and any
/// digipeater addresses (address chain ends when the extension bit is set).
/// </summary>
public static class Ax25Decoder
{
    public static Ax25Frame? Decode(byte[] frame)
    {
        // Minimum: dest(7) + source(7) + control(1) + pid(1) = 16 bytes.
        if (frame.Length < 16) return null;

        int offset = 0;
        var addresses = new List<(string call, int ssid)>();
        bool lastAddress = false;

        // The address chain is a sequence of 7-byte fields; the last one has the
        // low bit of its SSID byte set.
        while (offset + 7 <= frame.Length && !lastAddress && addresses.Count < 10)
        {
            var (call, ssid, last) = DecodeAddress(frame, offset);
            addresses.Add((call, ssid));
            lastAddress = last;
            offset += 7;
        }

        if (!lastAddress || addresses.Count < 2) return null; // malformed address chain
        if (offset + 2 > frame.Length) return null;           // no control+pid

        // control (0x03 UI), pid (0xF0) — skip them; the rest is the info field.
        offset += 2;
        string info = Encoding.ASCII.GetString(frame, offset, frame.Length - offset);

        var (dest, destSsid) = addresses[0];
        var (src, srcSsid) = addresses[1];
        var path = new List<string>();
        for (int i = 2; i < addresses.Count; i++)
            path.Add(addresses[i].ssid == 0 ? addresses[i].call : $"{addresses[i].call}-{addresses[i].ssid}");

        return new Ax25Frame(src, srcSsid, dest, destSsid, path, info);
    }

    private static (string call, int ssid, bool last) DecodeAddress(byte[] frame, int offset)
    {
        var sb = new StringBuilder(6);
        for (int i = 0; i < 6; i++)
        {
            char c = (char)(frame[offset + i] >> 1); // each callsign byte is left-shifted 1
            if (c != ' ') sb.Append(c);
        }
        byte ssidByte = frame[offset + 6];
        int ssid = (ssidByte >> 1) & 0x0F;
        bool last = (ssidByte & 0x01) != 0; // HDLC address-extension bit
        return (sb.ToString(), ssid, last);
    }
}

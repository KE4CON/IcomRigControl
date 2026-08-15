using System.Globalization;

namespace IcomRigControl.CivEngine;

public enum AprsPacketType { Position, Message, Status, Other }

/// <summary>
/// A parsed APRS information field: a position report (with lat/lon, symbol,
/// comment), a text message (addressee + text), a status, or other/unparsed.
/// </summary>
public record AprsPacket(
    AprsPacketType Type,
    double? Latitude,
    double? Longitude,
    char SymbolTable,
    char SymbolCode,
    string Comment,
    string? MessageAddressee,
    string? MessageText,
    string? MessageNumber = null);

/// <summary>
/// Parses an APRS information field (the AX.25 info bytes) into structured data.
/// Handles the common cases needed for an HF APRS station: uncompressed position
/// reports (!, =, /, @), text messages (:), and status (>). Compressed positions
/// and Mic-E are not parsed (rare on HF); those come back as Other with the raw
/// text in Comment. See CLAUDE.md HF APRS.
/// </summary>
public static class AprsParser
{
    public static AprsPacket Parse(string info)
    {
        if (string.IsNullOrEmpty(info))
            return Other(info ?? "");

        char dti = info[0];
        return dti switch
        {
            '!' or '=' => ParsePosition(info, timestamped: false),
            '/' or '@' => ParsePosition(info, timestamped: true),
            ':' => ParseMessage(info),
            '>' => new AprsPacket(AprsPacketType.Status, null, null, ' ', ' ', info[1..], null, null),
            _ => Other(info),
        };
    }

    private static AprsPacket ParsePosition(string info, bool timestamped)
    {
        // Skip the data-type indicator and, for @// forms, the 7-char timestamp.
        int i = 1 + (timestamped ? 7 : 0);

        // Uncompressed: DDMM.ssN <symTable> DDDMM.ssW <symCode> <comment>
        // = 8 + 1 + 9 + 1 = 19 chars of fixed field.
        if (info.Length < i + 19) return Other(info);

        string latField = info.Substring(i, 8);
        char symbolTable = info[i + 8];
        string lonField = info.Substring(i + 9, 9);
        char symbolCode = info[i + 18];
        string comment = info.Length > i + 19 ? info[(i + 19)..] : "";

        double? lat = ParseLatitude(latField);
        double? lon = ParseLongitude(lonField);
        if (lat is null || lon is null) return Other(info); // e.g. compressed form

        return new AprsPacket(AprsPacketType.Position, lat, lon, symbolTable, symbolCode, comment, null, null);
    }

    private static AprsPacket ParseMessage(string info)
    {
        // ":ADDRESSEE:message{msgno"  — addressee is 9 chars.
        if (info.Length < 11 || info[10] != ':') return Other(info);

        string addressee = info.Substring(1, 9).Trim();
        string text = info[11..];

        // A message may carry a line number as a "{nnnnn" suffix; keep it so the
        // receiver can send back an ":ackNN". A plain ACK has no such suffix.
        string? messageNumber = null;
        int brace = text.LastIndexOf('{');
        if (brace >= 0)
        {
            messageNumber = text[(brace + 1)..].Trim();
            text = text[..brace];
        }

        return new AprsPacket(AprsPacketType.Message, null, null, ' ', ' ', "", addressee, text, messageNumber);
    }

    private static double? ParseLatitude(string field)
    {
        // "DDMM.ssN"
        if (field.Length != 8) return null;
        if (!int.TryParse(field.AsSpan(0, 2), out int deg)) return null;
        if (!double.TryParse(field.AsSpan(2, 5), NumberStyles.Float, CultureInfo.InvariantCulture, out double min)) return null;
        char hemi = field[7];
        double value = deg + min / 60.0;
        if (hemi is 'S' or 's') value = -value;
        else if (hemi is not ('N' or 'n')) return null;
        return value;
    }

    private static double? ParseLongitude(string field)
    {
        // "DDDMM.ssW"
        if (field.Length != 9) return null;
        if (!int.TryParse(field.AsSpan(0, 3), out int deg)) return null;
        if (!double.TryParse(field.AsSpan(3, 5), NumberStyles.Float, CultureInfo.InvariantCulture, out double min)) return null;
        char hemi = field[8];
        double value = deg + min / 60.0;
        if (hemi is 'W' or 'w') value = -value;
        else if (hemi is not ('E' or 'e')) return null;
        return value;
    }

    private static AprsPacket Other(string info) =>
        new(AprsPacketType.Other, null, null, ' ', ' ', info, null, null);
}

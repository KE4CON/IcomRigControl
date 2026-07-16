using System.Globalization;

namespace IcomRigControl.CivEngine;

/// <summary>
/// Formats APRS position reports (the AX.25 UI frame's "information field")
/// per the TAPR APRS 1.0.1 spec's uncompressed position format:
/// !DDMM.MMN/DDDMM.MMWs[comment], where the leading "!" is the Data Type
/// Identifier for "position without timestamp", s is the symbol code, and
/// the "/" is the symbol table identifier (here always "/" for the primary
/// table, since a custom symbol table is out of scope for a beacon).
/// See CLAUDE.md Phase 10.
/// </summary>
public static class AprsPositionFormatter
{
    /// Formats a full APRS position report string ready to become an AX.25
    /// UI frame's info field.
    public static string FormatPosition(double latitude, double longitude, char symbolTable, char symbolCode, string comment)
    {
        if (latitude is < -90 or > 90)
        {
            throw new ArgumentOutOfRangeException(nameof(latitude), "Latitude must be -90 to 90.");
        }

        if (longitude is < -180 or > 180)
        {
            throw new ArgumentOutOfRangeException(nameof(longitude), "Longitude must be -180 to 180.");
        }

        string latStr = FormatLatitude(latitude);
        string lonStr = FormatLongitude(longitude);

        return $"!{latStr}{symbolTable}{lonStr}{symbolCode}{comment}";
    }

    /// Formats latitude as DDMM.MMN or DDMM.MMS — always exactly 8 characters.
    private static string FormatLatitude(double latitude)
    {
        char hemisphere = latitude >= 0 ? 'N' : 'S';
        double absLat = Math.Abs(latitude);

        int degrees = (int)absLat;
        double minutes = (absLat - degrees) * 60.0;

        return $"{degrees:00}{minutes:00.00}{hemisphere}";
    }

    /// Formats longitude as DDDMM.MME or DDDMM.MMW — always exactly 9 characters.
    private static string FormatLongitude(double longitude)
    {
        char hemisphere = longitude >= 0 ? 'E' : 'W';
        double absLon = Math.Abs(longitude);

        int degrees = (int)absLon;
        double minutes = (absLon - degrees) * 60.0;

        return $"{degrees:000}{minutes:00.00}{hemisphere}";
    }

    /// Builds a comment string for an operating-frequency beacon, e.g.
    /// for reporting "I'm on 14.074 MHz USB" as an APRS object comment.
    /// This is IcomRigControl-specific usage, not part of the base spec.
    public static string FormatFrequencyBeaconComment(long frequencyHz, string mode)
    {
        double mhz = frequencyHz / 1_000_000.0;
        return $"{mhz.ToString("0.000", CultureInfo.InvariantCulture)} MHz {mode}";
    }
}
using System.Globalization;
using System.Text.RegularExpressions;

namespace IcomRigControl.Services;

/// <summary>
/// Parses raw DX cluster text lines into DxSpot records. Cluster spot lines look
/// like:  "DX de W3LPL:      14074.0  K1ABC        FT8 -12 dB          1305Z"
/// Non-spot lines (banners, WWV, announcements, prompts) return null.
/// </summary>
public static partial class DxSpotParser
{
    // DX de <spotter>: <freq kHz> <dx call> <comment...> <timeZ>
    [GeneratedRegex(
        @"^DX de\s+(?<spotter>[^:\s]+):\s*(?<freq>\d+(?:\.\d+)?)\s+(?<dx>[A-Za-z0-9/]+)\s*(?<comment>.*?)\s*(?<time>\d{3,4}Z)?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SpotRegex();

    /// Parse a single cluster line into a DxSpot, or null if it isn't a spot.
    public static DxSpot? Parse(string? line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;

        var m = SpotRegex().Match(line.Trim());
        if (!m.Success) return null;

        if (!double.TryParse(m.Groups["freq"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double kHz))
            return null;

        long hz = (long)System.Math.Round(kHz * 1000.0);

        return new DxSpot(
            DxCallsign: m.Groups["dx"].Value.ToUpperInvariant(),
            FrequencyHz: hz,
            Spotter: m.Groups["spotter"].Value.ToUpperInvariant(),
            Comment: m.Groups["comment"].Value.Trim(),
            TimeUtc: m.Groups["time"].Value);
    }
}

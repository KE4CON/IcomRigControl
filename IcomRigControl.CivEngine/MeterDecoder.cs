namespace IcomRigControl.CivEngine;

/// <summary>
/// Decodes raw CI-V meter level bytes (2-byte BCD, 00 00 to 02 55) into real-world units.
/// All IC-7300 meters (S-meter, power, SWR, ALC, Vd, Id) share this same 0-255 raw scale
/// but map to different physical ranges.
/// </summary>
public static class MeterDecoder
{
    /// Decode a 2-byte CI-V level value to its raw 0-255 integer.
    public static int DecodeRawLevel(ReadOnlySpan<byte> data)
    {
        if (data.Length < 2) throw new ArgumentException("Meter data must be at least 2 bytes.");
        int hi = ((data[0] >> 4) & 0x0F) * 10 + (data[0] & 0x0F);
        int lo = ((data[1] >> 4) & 0x0F) * 10 + (data[1] & 0x0F);
        return hi * 100 + lo;
    }

    /// S-meter: 0-255 raw maps to S0-S9+60dB. Approximate dBm using standard S-unit steps (6dB/S-unit).
    public static (int sUnit, double dBm) DecodeSMeter(ReadOnlySpan<byte> data)
    {
        int raw = DecodeRawLevel(data);
        // IC-7300: 0=S0, 120=S9, 241=S9+60dB (per Icom S-meter calibration table)
        if (raw <= 120)
        {
            int sUnit = (int)Math.Round(raw / 120.0 * 9);
            double dBm = -73 + (sUnit * 6); // S9 = -73dBm reference, 6dB per S-unit
            return (sUnit, dBm);
        }
        else
        {
            double over9 = (raw - 120) / (241.0 - 120.0) * 60;
            double dBm = -73 + 54 + over9; // S9 baseline + dB over S9
            return (9, dBm);
        }
    }

    /// RF power / ALC / SWR-raw: 0-255 maps to 0-100% linear.
    public static double DecodePercent(ReadOnlySpan<byte> data)
    {
        int raw = DecodeRawLevel(data);
        return Math.Clamp(raw / 255.0 * 100.0, 0, 100);
    }

    /// SWR meter: 0-255 raw maps to SWR ratio 1.0-infinity (Icom's non-linear SWR scale).
    /// Approximation per Icom's published curve: raw 0=1.0, raw 48=1.5, raw 80=2.0, raw 120=3.0, raw 240=infinity(clamped to 10.0 for display)
    public static double DecodeSwr(ReadOnlySpan<byte> data)
    {
        int raw = DecodeRawLevel(data);
        if (raw <= 0) return 1.0;
        if (raw >= 240) return 10.0;

        // Piecewise linear approximation across Icom's known reference points
        (int raw, double swr)[] points =
        {
            (0, 1.0), (48, 1.5), (80, 2.0), (120, 3.0), (170, 5.0), (240, 10.0)
        };

        for (int i = 0; i < points.Length - 1; i++)
        {
            var (r0, s0) = points[i];
            var (r1, s1) = points[i + 1];
            if (raw >= r0 && raw <= r1)
            {
                double t = (raw - r0) / (double)(r1 - r0);
                return s0 + t * (s1 - s0);
            }
        }
        return 10.0;
    }

    /// Supply voltage (Vd): 0-255 raw maps to 0-16V per IC-7300 spec.
    public static double DecodeVoltage(ReadOnlySpan<byte> data)
    {
        int raw = DecodeRawLevel(data);
        return raw / 255.0 * 16.0;
    }

    /// Current draw (Id): 0-255 raw maps to 0-25A per IC-7300 spec.
    public static double DecodeCurrent(ReadOnlySpan<byte> data)
    {
        int raw = DecodeRawLevel(data);
        return raw / 255.0 * 25.0;
    }
}

namespace IcomRigControl.CivEngine;

/// <summary>
/// Decodes raw spectrum scope waveform data received from the IC-7300/MK2.
/// The radio sends up to 475 raw bytes, each 0-255 representing relative
/// signal level at that frequency point across the current scope span.
/// </summary>
public static class ScopeDataDecoder
{
    /// Decode a raw waveform byte buffer into an array of level values (0-255).
    public static int[] Decode(byte[] data)
    {
        var result = new int[data.Length];
        for (int i = 0; i < data.Length; i++)
        {
            result[i] = data[i];
        }
        return result;
    }

    /// Convert a single raw level (0-255) to a 0-100% scale for display.
    public static double NormalizeToPercent(int rawLevel)
    {
        return System.Math.Clamp(rawLevel / 255.0 * 100.0, 0, 100);
    }
}
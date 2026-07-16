using System.Globalization;

namespace IcomRigControl.CivEngine;

/// <summary>
/// One axis label: a frequency and the pixel x-position it corresponds to.
/// </summary>
public record AxisLabel(long FrequencyHz, double PixelX);

/// <summary>
/// Maps between waterfall data-point pixel positions and actual frequencies,
/// given the scope's current center frequency and span. Used for both the
/// frequency axis labels above the waterfall and click-to-tune. See
/// CLAUDE.md Phase 7.
/// </summary>
public static class WaterfallFrequencyMapper
{
    /// Converts a pixel x-position (0 to dataPointCount-1) into the frequency
    /// it represents, assuming Center scope mode (centerFrequencyHz sits at
    /// the middle data point, spanHz is spread evenly left/right of it).
    public static long PixelToFrequency(int pixelX, int dataPointCount, long centerFrequencyHz, long spanHz)
    {
        double fraction = dataPointCount <= 1 ? 0.5 : (double)pixelX / (dataPointCount - 1);
        long lowEdge = centerFrequencyHz - spanHz / 2;
        return lowEdge + (long)(fraction * spanHz);
    }

    /// Generates a set of evenly-spaced axis labels across the current span,
    /// with pixel positions expressed as a 0.0-1.0 fraction of the waterfall
    /// width (so the UI can multiply by actual rendered width).
    public static List<AxisLabel> GenerateAxisLabels(long centerFrequencyHz, long spanHz, int labelCount)
    {
        var labels = new List<AxisLabel>();
        if (labelCount < 1) return labels;

        long lowEdge = centerFrequencyHz - spanHz / 2;

        for (int i = 0; i < labelCount; i++)
        {
            double fraction = labelCount == 1 ? 0.5 : (double)i / (labelCount - 1);
            long freq = lowEdge + (long)(fraction * spanHz);
            labels.Add(new AxisLabel(freq, fraction));
        }

        return labels;
    }

    /// Formats a frequency in Hz as a human-readable MHz string, e.g.
    /// 14074000 -> "14.074.000" matching the main dashboard's display style,
    /// but more compact for axis labels: "14.074.000" is too wide for a
    /// small axis tick, so this uses "14.0740" (MHz with 4 decimal places).
    public static string FormatFrequencyLabel(long frequencyHz)
    {
        double mhz = frequencyHz / 1_000_000.0;
        return mhz.ToString("0.0000", CultureInfo.InvariantCulture);
    }
}
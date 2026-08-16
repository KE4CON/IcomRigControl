namespace IcomRigControl.Services;

/// <summary>
/// Computes sunrise/sunset (the "grey line" terminator) for a location — the basis of
/// grey-line DX timing, where the low bands open along the sunrise/sunset line. Uses
/// the standard sunrise equation (NOAA/Wikipedia); returns null at latitudes where the
/// sun doesn't rise or set on the date (polar day/night). Pure math, unit-testable.
/// See CLAUDE.md grey line.
/// </summary>
public static class GreyLine
{
    private const double Deg = Math.PI / 180.0;

    /// Sunrise and sunset in UTC for the given location and date, or null for polar
    /// day/night. Longitude is east-positive (the usual convention).
    public static (DateTime SunriseUtc, DateTime SunsetUtc)? SunriseSunset(double latDeg, double lonDeg, DateTime dateUtc)
    {
        double lw = -lonDeg; // the equation uses west-positive longitude
        double jDate = ToJulian(dateUtc.Date);

        double n = Math.Round(jDate - 2451545.0 - 0.0009 + lw / 360.0);
        double jStar = 2451545.0 + 0.0009 + lw / 360.0 + n;     // mean solar noon

        double m = Mod360(357.5291 + 0.98560028 * (jStar - 2451545.0));
        double c = 1.9148 * Math.Sin(m * Deg) + 0.0200 * Math.Sin(2 * m * Deg) + 0.0003 * Math.Sin(3 * m * Deg);
        double lambda = Mod360(m + c + 180 + 102.9372);

        double jTransit = jStar + 0.0053 * Math.Sin(m * Deg) - 0.0069 * Math.Sin(2 * lambda * Deg);

        double sinDelta = Math.Sin(lambda * Deg) * Math.Sin(23.44 * Deg);
        double delta = Math.Asin(sinDelta);

        double cosOmega = (Math.Sin(-0.833 * Deg) - Math.Sin(latDeg * Deg) * sinDelta) /
                          (Math.Cos(latDeg * Deg) * Math.Cos(delta));
        if (cosOmega > 1 || cosOmega < -1) return null; // never rises / never sets

        double omega = Math.Acos(cosOmega) / Deg; // degrees
        double jRise = jTransit - omega / 360.0;
        double jSet = jTransit + omega / 360.0;

        return (FromJulian(jRise), FromJulian(jSet));
    }

    private static double Mod360(double x) => ((x % 360) + 360) % 360;
    private static double ToJulian(DateTime utc) => utc.ToOADate() + 2415018.5;
    private static DateTime FromJulian(double jd) => DateTime.SpecifyKind(DateTime.FromOADate(jd - 2415018.5), DateTimeKind.Utc);

    // ── Grey-line windows (design adapted from the ActivationPlanner project) ──

    public const double DefaultWindowHours = 0.75; // ±45 min of enhanced propagation

    /// The grey-line windows spanning yesterday→tomorrow (so "current"/"next" resolve
    /// across UTC midnight), chronologically, deduped by event-minute.
    public static List<GreyLinePeriod> Windows(double latDeg, double lonDeg, DateTime nowUtc,
        double windowHours = DefaultWindowHours)
    {
        var periods = new List<GreyLinePeriod>();
        for (int day = -1; day <= 1; day++)
        {
            var r = SunriseSunset(latDeg, lonDeg, nowUtc.Date.AddDays(day));
            if (r is not { } rs) continue;
            periods.Add(new GreyLinePeriod(GreyLineEventKind.Sunrise,
                rs.SunriseUtc.AddHours(-windowHours), rs.SunriseUtc, rs.SunriseUtc.AddHours(windowHours)));
            periods.Add(new GreyLinePeriod(GreyLineEventKind.Sunset,
                rs.SunsetUtc.AddHours(-windowHours), rs.SunsetUtc, rs.SunsetUtc.AddHours(windowHours)));
        }
        return periods
            .GroupBy(p => (p.Kind, p.EventUtc.Ticks / TimeSpan.TicksPerMinute))
            .Select(g => g.First())
            .OrderBy(p => p.StartUtc)
            .ToList();
    }

    /// Whether a grey-line window is active now (with time left) or the next one is
    /// coming (with time until it starts).
    public static GreyLineStatus StatusAt(double latDeg, double lonDeg, DateTime nowUtc,
        double windowHours = DefaultWindowHours)
    {
        var windows = Windows(latDeg, lonDeg, nowUtc, windowHours);
        var current = windows.FirstOrDefault(p => p.Contains(nowUtc));
        if (current is not null)
            return new GreyLineStatus(true, current, null, null, current.EndUtc - nowUtc);

        var next = windows.FirstOrDefault(p => p.StartUtc > nowUtc);
        return new GreyLineStatus(false, null, next, next is null ? null : next.StartUtc - nowUtc, null);
    }
}

public enum GreyLineEventKind { Sunrise, Sunset }

/// One grey-line window: event ± a half-window, in UTC.
public sealed record GreyLinePeriod(GreyLineEventKind Kind, DateTime StartUtc, DateTime EventUtc, DateTime EndUtc)
{
    public bool Contains(DateTime utc) => utc >= StartUtc && utc <= EndUtc;
}

/// The grey-line state at a moment.
public sealed record GreyLineStatus(bool IsActive, GreyLinePeriod? Current, GreyLinePeriod? Next,
    TimeSpan? UntilStart, TimeSpan? UntilEnd);

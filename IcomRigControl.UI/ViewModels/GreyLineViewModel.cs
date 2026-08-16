using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IcomRigControl.Services;

namespace IcomRigControl.UI.ViewModels;

/// <summary>
/// Grey-line DX timing: your sunrise/sunset (the terminator), when the low bands tend
/// to open along it, and whether you're in a grey-line window right now. Uses your
/// location from Settings (the APRS latitude/longitude). See CLAUDE.md grey line.
/// </summary>
public partial class GreyLineViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;

    [ObservableProperty] private string _location = "";
    [ObservableProperty] private string _sunrise = "";
    [ObservableProperty] private string _sunset = "";
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool _inGreyLine;
    [ObservableProperty] private string _note = "Grey-line propagation: the 160/80/40m bands often open long-distance along your sunrise and sunset line.";

    public GreyLineViewModel(SettingsService settingsService)
    {
        _settingsService = settingsService;
        Refresh();
    }

    [RelayCommand]
    private void Refresh()
    {
        var s = _settingsService.Load();
        double lat = s.AprsLatitude, lon = s.AprsLongitude;
        if (lat == 0 && lon == 0)
        {
            Location = "Location not set";
            StatusText = "Set your latitude/longitude in Settings (the APRS location) to compute the grey line.";
            Sunrise = Sunset = "";
            return;
        }

        Location = $"{lat:F3}, {lon:F3}";
        var now = DateTime.UtcNow;
        var r = GreyLine.SunriseSunset(lat, lon, now);
        if (r is not { } rs)
        {
            StatusText = "The sun doesn't rise or set at your location today (polar day/night).";
            Sunrise = Sunset = "—";
            return;
        }

        Sunrise = $"{rs.SunriseUtc:HH:mm} UTC  ({rs.SunriseUtc.ToLocalTime():HH:mm} local)";
        Sunset = $"{rs.SunsetUtc:HH:mm} UTC  ({rs.SunsetUtc.ToLocalTime():HH:mm} local)";

        // Grey-line windows (event ± ~45 min) with an active-now / next-in countdown.
        var st = GreyLine.StatusAt(lat, lon, now);
        InGreyLine = st.IsActive;
        if (st.IsActive && st.Current is { } c)
            StatusText = $"★ GREY LINE NOW — {c.Kind} window, {Fmt(st.UntilEnd)} left. Try the low bands (160/80/40m) for long-haul DX!";
        else if (st.Next is { } n)
            StatusText = $"Next grey line: {n.Kind.ToString().ToLowerInvariant()} window in {Fmt(st.UntilStart)} (at {n.StartUtc:HH:mm} UTC).";
        else
            StatusText = now > rs.SunriseUtc && now < rs.SunsetUtc ? "Daytime." : "Nighttime.";
    }

    private static string Fmt(TimeSpan? t) =>
        t is not { } ts ? "?" : ts.TotalHours >= 1 ? $"{(int)ts.TotalHours}h {ts.Minutes}m" : $"{ts.Minutes}m";
}

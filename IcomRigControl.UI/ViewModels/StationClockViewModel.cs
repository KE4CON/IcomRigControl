using System;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IcomRigControl.Services;

namespace IcomRigControl.UI.ViewModels;

/// <summary>
/// The dashboard clock (local + UTC, updated each second) and the FCC station-ID
/// reminder. When enabled, counts down the interval and raises an unmissable banner
/// at zero until the operator taps "Identified". See CLAUDE.md ID timer.
/// </summary>
public partial class StationClockViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private readonly IdReminderTimer _idTimer;
    private readonly DispatcherTimer _tick;

    [ObservableProperty] private string _localTime = "";
    [ObservableProperty] private string _utcTime = "";
    [ObservableProperty] private bool _idTimerEnabled;
    [ObservableProperty] private string _idCountdown = "10:00";
    [ObservableProperty] private bool _idDue;
    [ObservableProperty] private string _timeSyncStatus = "";

    public StationClockViewModel(SettingsService settingsService)
    {
        _settingsService = settingsService;
        var s = settingsService.Load();
        _idTimer = new IdReminderTimer(Math.Max(1, s.IdTimerMinutes) * 60);
        _idTimerEnabled = s.IdTimerEnabled;
        _idCountdown = _idTimer.Display;

        _idTimer.Elapsed += () => Dispatcher.UIThread.Post(() => IdDue = true);

        _tick = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _tick.Tick += OnTick;
        _tick.Start();
        UpdateClocks();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        UpdateClocks();
        if (IdTimerEnabled)
        {
            _idTimer.Tick();
            IdCountdown = _idTimer.Display;
        }
    }

    private void UpdateClocks()
    {
        var now = DateTime.Now;
        LocalTime = now.ToString("ddd HH:mm:ss");
        UtcTime = DateTime.UtcNow.ToString("HH:mm:ss") + "Z";
    }

    partial void OnIdTimerEnabledChanged(bool value)
    {
        var s = _settingsService.Load();
        s.IdTimerEnabled = value;
        _settingsService.Save(s);
        _idTimer.Reset();
        IdCountdown = _idTimer.Display;
        IdDue = false;
    }

    /// The operator identified — restart the 10-minute clock and clear the banner.
    [RelayCommand]
    private void AcknowledgeId()
    {
        _idTimer.Acknowledge();
        IdCountdown = _idTimer.Display;
        IdDue = false;
    }

    /// Compares the PC clock to internet (NTP) time and reports the offset.
    [RelayCommand]
    private async System.Threading.Tasks.Task CheckTime()
    {
        TimeSyncStatus = "Checking…";
        TimeSpan? offset = await NtpClient.GetOffsetAsync();
        if (offset is not { } o) { TimeSyncStatus = "Couldn't reach a time server."; return; }
        double s = o.TotalSeconds;
        TimeSyncStatus = Math.Abs(s) < 1
            ? $"PC clock is accurate (±{Math.Abs(s):F1}s of internet time)."
            : $"PC clock is {Math.Abs(s):F1}s {(s > 0 ? "behind" : "ahead of")} internet time — consider syncing it.";
    }
}

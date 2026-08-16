using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IcomRigControl.Services;

namespace IcomRigControl.UI.ViewModels;

/// <summary>
/// Manages scheduled station actions (nets / unattended operation): add, list, and
/// remove tasks that fire at a UTC time each day. Changes persist to settings and are
/// pushed to the live Scheduler via the supplied callback. See CLAUDE.md scheduler.
/// </summary>
public partial class SchedulerViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private readonly Action _onChanged;

    [ObservableProperty] private string _newTime = "00:00";
    [ObservableProperty] private string _selectedAction = "Frequency";
    [ObservableProperty] private string _newParameter = "";
    [ObservableProperty] private string _status = "Times are UTC. Tasks run once a day.";

    public string[] Actions { get; } = { "Frequency", "Mode", "Beacon", "RecordStart", "RecordStop", "PowerOn", "PowerOff" };
    public ObservableCollection<ScheduledTask> Tasks { get; } = new();

    public SchedulerViewModel(SettingsService settingsService, Action onChanged)
    {
        _settingsService = settingsService;
        _onChanged = onChanged;
        foreach (var t in settingsService.Load().ScheduledTasks) Tasks.Add(t);
    }

    [RelayCommand]
    private void Add()
    {
        if (!TimeOnly.TryParse(NewTime, out _)) { Status = "Enter a valid time as HH:mm (UTC)."; return; }
        var task = new ScheduledTask(Guid.NewGuid().ToString("N"), NewTime, SelectedAction, NewParameter.Trim());
        Tasks.Add(task);
        Persist();
        Status = $"Added: {task.TimeUtc} UTC → {task.Action} {task.Parameter}";
    }

    [RelayCommand]
    private void Remove(ScheduledTask? task)
    {
        if (task is null) return;
        Tasks.Remove(task);
        Persist();
    }

    private void Persist()
    {
        var s = _settingsService.Load();
        s.ScheduledTasks = Tasks.ToList();
        _settingsService.Save(s);
        _onChanged();
    }
}

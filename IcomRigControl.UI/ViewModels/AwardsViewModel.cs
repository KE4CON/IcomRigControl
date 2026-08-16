using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IcomRigControl.Services;

namespace IcomRigControl.UI.ViewModels;

/// <summary>
/// Award progress from your worked contacts — this app's log plus your HRD logbook:
/// DXCC entities and grid squares worked, with the DXCC 100-entity goal. See
/// CLAUDE.md award tracking.
/// </summary>
public partial class AwardsViewModel : ViewModelBase
{
    private readonly QsoLogger _qsoLogger;
    private readonly SettingsService _settingsService;

    [ObservableProperty] private int _entityCount;
    [ObservableProperty] private int _gridCount;
    [ObservableProperty] private int _stateCount;
    [ObservableProperty] private string _dxccStatus = "";
    [ObservableProperty] private string _wasStatus = "";
    [ObservableProperty] private string _status = "Loading…";

    public ObservableCollection<string> Entities { get; } = new();

    public AwardsViewModel(QsoLogger qsoLogger, SettingsService settingsService)
    {
        _qsoLogger = qsoLogger;
        _settingsService = settingsService;
        _ = RefreshAsync();
    }

    [RelayCommand]
    private async Task Refresh() => await RefreshAsync();

    private async Task RefreshAsync()
    {
        Status = "Loading…";
        var contacts = new List<WorkedContact>(
            _qsoLogger.Qsos.Select(q => new WorkedContact(q.Callsign, q.Band, q.GridSquare, q.State)));

        var s = _settingsService.Load();
        int hrd = 0;
        if (s.HrdBridgeEnabled && !string.IsNullOrWhiteSpace(s.HrdDatabasePath))
        {
            try
            {
                var fromHrd = await new HrdSqliteBridge(s.HrdDatabasePath).ReadWorkedAsync();
                hrd = fromHrd.Count;
                contacts.AddRange(fromHrd);
            }
            catch { /* HRD unreachable */ }
        }

        var tracker = new AwardTracker(contacts);
        EntityCount = tracker.EntityCount;
        GridCount = tracker.GridCount;
        StateCount = tracker.StateCount;
        DxccStatus = tracker.EntityCount >= 100
            ? $"{tracker.EntityCount} / 100 entities — DXCC achieved! 🎉"
            : $"{tracker.EntityCount} / 100 entities for DXCC ({100 - tracker.EntityCount} to go)";
        WasStatus = tracker.StateCount >= 50
            ? "50 / 50 states — WAS achieved! 🎉"
            : $"{tracker.StateCount} / 50 states for WAS ({50 - tracker.StateCount} to go)";

        Entities.Clear();
        foreach (string e in tracker.Entities.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            Entities.Add(e);

        Status = hrd > 0
            ? $"From {contacts.Count} contacts (this app + {hrd} from HRD)."
            : $"From {contacts.Count} contacts (this app's log — enable the HRD bridge in Settings to include your HRD history).";
    }
}

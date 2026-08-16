using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IcomRigControl.RigModel;
using IcomRigControl.Services;

namespace IcomRigControl.UI.ViewModels;

/// A selectable DX cluster node. ToString returns Name so it shows nicely in the
/// ComboBox; the "Custom" entry lets the user type their own host/port.
public record ClusterPreset(string Name, string Host, int Port)
{
    public override string ToString() => Name;
}

/// <summary>
/// DX Cluster window: connects to a cluster node, shows incoming spots (newest
/// first — a band map), and tunes the radio to a spot on click. The spot list
/// is capped so a busy cluster can't grow it without bound.
/// </summary>
public partial class DxClusterViewModel : ViewModelBase, IAsyncDisposable
{
    private const int MaxSpots = 200;

    private readonly Transceiver _transceiver;
    private readonly SettingsService _settingsService;
    private readonly QsoLogger _qsoLogger;
    private DxClusterService? _service;

    public ObservableCollection<DxSpot> Spots { get; } = new();

    /// When on, only spots you haven't worked on that band are shown.
    [ObservableProperty] private bool _newOnly;
    /// How many "new" (not-yet-worked-on-band) spots are currently listed.
    [ObservableProperty] private int _newCount;

    /// The "type your own host/port" entry.
    public static readonly ClusterPreset Custom = new("Custom / manual entry", "", 0);

    /// A short list of well-known public clusters, plus Custom. RBN nodes are
    /// automated skimmer spots (great for CW/RTTY/FT8 activity).
    public List<ClusterPreset> AvailableClusters { get; } = new()
    {
        new("NC7J", "dxc.nc7j.com", 7373),
        new("VE7CC (CC Cluster)", "dxc.ve7cc.net", 23),
        new("DXFun Cluster", "dxfun.com", 8000),
        new("Reverse Beacon Network — CW/RTTY", "telnet.reversebeacon.net", 7000),
        new("Reverse Beacon Network — FT8/FT4", "telnet.reversebeacon.net", 7001),
        Custom,
    };

    [ObservableProperty] private ClusterPreset? _selectedCluster;

    [ObservableProperty] private string _host;
    [ObservableProperty] private int _port;
    [ObservableProperty] private string _loginCall;
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private string _status = "Not connected. Enter a cluster host and your callsign, then Connect.";

    // ── Post-a-spot inputs ───────────────────────────────────────────────
    [ObservableProperty] private string _spotCallsign = "";
    [ObservableProperty] private string _spotFrequencyKHz = "";
    [ObservableProperty] private string _spotComment = "";

    public DxClusterViewModel(Transceiver transceiver, SettingsService settingsService, QsoLogger qsoLogger)
    {
        _transceiver = transceiver;
        _settingsService = settingsService;
        _qsoLogger = qsoLogger;

        var settings = _settingsService.Load();
        _host = settings.DxClusterHost;
        _port = settings.DxClusterPort;
        _loginCall = settings.DxClusterLoginCall;

        // Prefill the spot frequency with the radio's current frequency.
        _spotFrequencyKHz = (_transceiver.FrequencyHz / 1000.0).ToString("F1", CultureInfo.InvariantCulture);

        // Preselect a preset matching the saved host/port, otherwise Custom.
        _selectedCluster = AvailableClusters.FirstOrDefault(c => c.Host == _host && c.Port == _port) ?? Custom;
    }

    partial void OnSelectedClusterChanged(ClusterPreset? value)
    {
        // Picking a named preset fills in host/port; Custom leaves them editable.
        if (value is null || ReferenceEquals(value, Custom)) return;
        Host = value.Host;
        Port = value.Port;
    }

    [RelayCommand]
    private async Task Connect()
    {
        if (string.IsNullOrWhiteSpace(Host)) { Status = "Enter a cluster host (e.g. dxc.nc7j.com)."; return; }
        if (string.IsNullOrWhiteSpace(LoginCall)) { Status = "Enter your login callsign."; return; }

        // Persist the connection details for next time.
        var settings = _settingsService.Load();
        settings.DxClusterHost = Host;
        settings.DxClusterPort = Port;
        settings.DxClusterLoginCall = LoginCall;
        _settingsService.Save(settings);

        await LoadWorkedEntitiesAsync(settings); // for "new DXCC" alerts

        await DisposeServiceAsync();

        _service = new DxClusterService(LoginCall);
        _service.SpotReceived += OnSpotReceived;
        _service.StatusChanged += OnStatusChanged;
        await _service.ConnectAsync(Host, Port);
        IsConnected = _service.IsConnected;
    }

    [RelayCommand]
    private async Task Disconnect()
    {
        await DisposeServiceAsync();
        IsConnected = false;
        Status = "Disconnected.";
    }

    [RelayCommand]
    private async Task TuneToSpot(DxSpot? spot)
    {
        if (spot is null) return;
        try
        {
            await _transceiver.SetFrequencyAsync(spot.FrequencyHz);
            Status = $"Tuned to {spot.DxCallsign} on {spot.FrequencyKHz:F1} kHz.";
        }
        catch (Exception ex)
        {
            Status = $"Tune error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ClearSpots() => Spots.Clear();

    /// Fill the spot frequency box from the radio's current frequency.
    [RelayCommand]
    private void UseRadioFrequency() =>
        SpotFrequencyKHz = (_transceiver.FrequencyHz / 1000.0).ToString("F1", CultureInfo.InvariantCulture);

    /// Post (announce) a spot to the cluster: DX &lt;freq&gt; &lt;call&gt; &lt;comment&gt;.
    [RelayCommand]
    private async Task PostSpot()
    {
        if (_service is null || !_service.IsConnected)
        {
            Status = "Connect to a cluster before posting a spot.";
            return;
        }
        if (string.IsNullOrWhiteSpace(SpotCallsign))
        {
            Status = "Enter the DX callsign to spot.";
            return;
        }
        if (!double.TryParse(SpotFrequencyKHz, NumberStyles.Float, CultureInfo.InvariantCulture, out double kHz) || kHz <= 0)
        {
            Status = "Enter a valid frequency in kHz (e.g. 14074.0).";
            return;
        }

        string call = SpotCallsign.Trim().ToUpperInvariant();
        await _service.PostSpotAsync(kHz, call, SpotComment);
        Status = $"Posted spot: {call} on {kHz.ToString("F1", CultureInfo.InvariantCulture)} kHz.";
    }

    // These events fire on the cluster read-loop thread — marshal to the UI thread
    // before touching the bound collection (see CLAUDE.md's UI-thread rule).
    // DXCC entities already worked, from HRD's whole log (cached) plus this app's log.
    private readonly HashSet<string> _hrdEntities = new(StringComparer.OrdinalIgnoreCase);

    private async Task LoadWorkedEntitiesAsync(AppSettings settings)
    {
        _hrdEntities.Clear();
        if (!settings.HrdBridgeEnabled || string.IsNullOrWhiteSpace(settings.HrdDatabasePath)) return;
        try
        {
            var hrd = new HrdSqliteBridge(settings.HrdDatabasePath);
            foreach (var e in new AwardTracker(await hrd.ReadWorkedAsync()).Entities) _hrdEntities.Add(e);
            Status = $"Loaded {_hrdEntities.Count} worked DXCC entities from HRD for new-one alerts.";
        }
        catch { /* HRD unreachable — alerts fall back to this app's log only */ }
    }

    // A spot is a "new one" if its DXCC entity isn't in HRD's history or this session.
    private bool IsNewEntity(string call)
    {
        string entity = DxccResolver.Resolve(call);
        if (entity == "Unknown" || _hrdEntities.Contains(entity)) return false;
        foreach (var q in _qsoLogger.Qsos)
            if (DxccResolver.Resolve(q.Callsign).Equals(entity, StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

    private readonly PushNotifier _push = new(new System.Net.Http.HttpClient());
    private readonly HashSet<string> _pushedEntities = new(StringComparer.OrdinalIgnoreCase);

    private void OnSpotReceived(object? sender, DxSpot spot) =>
        Dispatcher.UIThread.Post(() =>
        {
            spot.IsNew = IsNewEntity(spot.DxCallsign); // new DXCC entity = "new one!"

            // Push a phone alert for a genuinely new entity (once per entity per session).
            if (spot.IsNew)
            {
                string entity = DxccResolver.Resolve(spot.DxCallsign);
                if (_pushedEntities.Add(entity))
                {
                    var s = _settingsService.Load();
                    if (s.PushEnabled && !string.IsNullOrWhiteSpace(s.PushTopic))
                        _ = _push.SendAsync(s.PushTopic, $"New one! {entity}",
                            $"{spot.DxCallsign} spotted on {spot.FrequencyKHz:F1} kHz — you haven't worked {entity}.");
                }
            }

            if (NewOnly && !spot.IsNew) return; // filtered out

            Spots.Insert(0, spot);
            while (Spots.Count > MaxSpots) Spots.RemoveAt(Spots.Count - 1);
            NewCount = Spots.Count(s => s.IsNew);
        });

    private void OnStatusChanged(object? sender, string message) =>
        Dispatcher.UIThread.Post(() =>
        {
            Status = message;
            IsConnected = _service?.IsConnected ?? false;
        });

    private async Task DisposeServiceAsync()
    {
        if (_service is not null)
        {
            _service.SpotReceived -= OnSpotReceived;
            _service.StatusChanged -= OnStatusChanged;
            await _service.DisconnectAsync();
            _service = null;
        }
    }

    public async ValueTask DisposeAsync() => await DisposeServiceAsync();
}

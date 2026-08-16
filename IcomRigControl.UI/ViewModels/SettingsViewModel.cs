using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IcomRigControl.Services;

namespace IcomRigControl.UI.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    /// Raised after a successful save, so the View can close the window.
    public event EventHandler? RequestClose;

    private readonly SettingsService _settingsService;
    private readonly IAudioPlayer _audioPlayer = AudioDevices.CreatePlayer();

    // ── Radio model ──────────────────────────────────────────────────────
    [ObservableProperty]
    private string _selectedRadioModel = "IC7300";

    // Names match the RadioModel enum exactly so they parse back with no mapping.
    public List<string> AvailableRadioModels { get; } = new() { "IC7300", "IC7300MK2", "IC705" };

    // ── Phase 9: Connection mode ────────────────────────────────────────
    [ObservableProperty]
    private string _connectionMode = "Demo";

    public List<string> AvailableConnectionModes { get; } = new() { "Demo", "Serial", "Remote" };

    [ObservableProperty]
    private string _serialPortName = "";

    // Serial ports the OS currently sees. Refreshable, since plugging/unplugging a
    // USB radio changes what's available.
    public System.Collections.ObjectModel.ObservableCollection<string> AvailablePorts { get; } = new();

    [RelayCommand]
    private void RefreshPorts()
    {
        AvailablePorts.Clear();
        foreach (string p in SerialPorts.List()) AvailablePorts.Add(p);
    }

    [ObservableProperty]
    private string _remoteHost = "";

    [ObservableProperty]
    private int _remotePort = 7300;

    [ObservableProperty]
    private string _remoteAuthToken = "";

    // ── Phase 10: APRS beacon settings ──────────────────────────────────
    [ObservableProperty]
    private string _aprsCallsign = "";

    [ObservableProperty]
    private int _aprsSsid = 9;

    [ObservableProperty]
    private string _aprsSymbolTable = "/";

    [ObservableProperty]
    private string _aprsSymbolCode = ">";

    [ObservableProperty]
    private string _aprsComment = "";

    [ObservableProperty]
    private double _aprsLatitude;

    [ObservableProperty]
    private double _aprsLongitude;

    [ObservableProperty]
    private string _audioOutputDeviceName = "";

    [ObservableProperty]
    private int _aprsBeaconIntervalMinutes = 10;

    public List<string> AvailableAudioDevices { get; }

    [ObservableProperty]
    private string _callsignLookupSource = "Callook";

    public List<string> AvailableLookupSources { get; } = new() { "Callook", "QRZ", "HamQTH" };

    [ObservableProperty]
    private string _qrzUsername = "";

    [ObservableProperty]
    private string _qrzPassword = "";

    [ObservableProperty]
    private string _hamQthUsername = "";

    [ObservableProperty]
    private string _hamQthPassword = "";

    [ObservableProperty]
    private string _tqslExecutablePath = "";

    [ObservableProperty]
    private bool _hrdBridgeEnabled;

    [ObservableProperty]
    private string _hrdDatabasePath = "";

    [ObservableProperty]
    private bool _n1mmSendEnabled;

    [ObservableProperty]
    private bool _n1mmReceiveEnabled;

    [ObservableProperty]
    private string _n1mmDestinationInput = "127.0.0.1:12060";

    [ObservableProperty]
    private int _contactListenPort = 12070;

    [ObservableProperty]
    private string _statusMessage = "";

    public SettingsViewModel(SettingsService settingsService)
    {
        _settingsService = settingsService;

        // Populate the audio device list once at startup; a "System Default"
        // option is prepended so the user can explicitly choose "let Windows
        // decide" rather than always requiring a specific named device.
        var devices = new List<string> { "(System Default)" };
        devices.AddRange(_audioPlayer.GetAvailableDevices());
        AvailableAudioDevices = devices;

        RefreshPorts(); // list serial ports (incl. a paired Bluetooth radio)

        LoadFromSettings(_settingsService.Load());
    }

    private void LoadFromSettings(AppSettings settings)
    {
        SelectedRadioModel = AvailableRadioModels.Contains(settings.RadioModel)
            ? settings.RadioModel
            : "IC7300";
        ConnectionMode = settings.ConnectionMode;
        SerialPortName = settings.SerialPortName;
        RemoteHost = settings.RemoteHost;
        RemotePort = settings.RemotePort;
        RemoteAuthToken = settings.RemoteAuthToken;

        AprsCallsign = settings.AprsCallsign;
        AprsSsid = settings.AprsSsid;
        AprsSymbolTable = settings.AprsSymbolTable.ToString();
        AprsSymbolCode = settings.AprsSymbolCode.ToString();
        AprsComment = settings.AprsComment;
        AprsLatitude = settings.AprsLatitude;
        AprsLongitude = settings.AprsLongitude;
        AudioOutputDeviceName = string.IsNullOrWhiteSpace(settings.AudioOutputDeviceName)
            ? "(System Default)"
            : settings.AudioOutputDeviceName;
        AprsBeaconIntervalMinutes = settings.AprsBeaconIntervalMinutes;

        CallsignLookupSource = settings.CallsignLookupSource;
        QrzUsername = settings.QrzUsername;
        QrzPassword = settings.QrzPassword;
        HamQthUsername = settings.HamQthUsername;
        HamQthPassword = settings.HamQthPassword;
        TqslExecutablePath = settings.TqslExecutablePath;
        HrdBridgeEnabled = settings.HrdBridgeEnabled;
        HrdDatabasePath = settings.HrdDatabasePath;
        N1mmSendEnabled = settings.N1mmSendEnabled;
        N1mmReceiveEnabled = settings.N1mmReceiveEnabled;
        ContactListenPort = settings.ContactListenPort;

        if (settings.N1mmDestinations.Count > 0)
        {
            var (ip, port) = settings.N1mmDestinations[0];
            N1mmDestinationInput = $"{ip}:{port}";
        }
    }

    [RelayCommand]
    private void SaveSettings()
    {
        try
        {
            var destinations = new List<(string Ip, int Port)>();
            var parts = N1mmDestinationInput.Split(':');
            if (parts.Length == 2 && int.TryParse(parts[1], out int port))
            {
                destinations.Add((parts[0], port));
            }

            char symbolTable = string.IsNullOrEmpty(AprsSymbolTable) ? '/' : AprsSymbolTable[0];
            char symbolCode = string.IsNullOrEmpty(AprsSymbolCode) ? '>' : AprsSymbolCode[0];

            var settings = new AppSettings
            {
                RadioModel = SelectedRadioModel,
                ConnectionMode = ConnectionMode,
                SerialPortName = SerialPortName,
                RemoteHost = RemoteHost,
                RemotePort = RemotePort,
                RemoteAuthToken = RemoteAuthToken,

                AprsCallsign = AprsCallsign,
                AprsSsid = AprsSsid,
                AprsSymbolTable = symbolTable,
                AprsSymbolCode = symbolCode,
                AprsComment = AprsComment,
                AprsLatitude = AprsLatitude,
                AprsLongitude = AprsLongitude,
                AudioOutputDeviceName = AudioOutputDeviceName == "(System Default)" ? "" : AudioOutputDeviceName,
                AprsBeaconIntervalMinutes = AprsBeaconIntervalMinutes,

                CallsignLookupSource = CallsignLookupSource,
                QrzUsername = QrzUsername,
                QrzPassword = QrzPassword,
                HamQthUsername = HamQthUsername,
                HamQthPassword = HamQthPassword,
                TqslExecutablePath = TqslExecutablePath,
                HrdBridgeEnabled = HrdBridgeEnabled,
                HrdDatabasePath = HrdDatabasePath,
                N1mmSendEnabled = N1mmSendEnabled,
                N1mmReceiveEnabled = N1mmReceiveEnabled,
                N1mmDestinations = destinations,
                ContactListenPort = ContactListenPort
            };

            _settingsService.Save(settings);
            StatusMessage = "Settings saved. Connection mode changes require an app restart to take effect.";
            RequestClose?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving settings: {ex.Message}";
        }
    }
}
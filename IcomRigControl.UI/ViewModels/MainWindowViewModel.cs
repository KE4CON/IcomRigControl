using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IcomRigControl.CivEngine;
using IcomRigControl.RigModel;
using IcomRigControl.Services;
using IcomRigControl.UI.Demo;

namespace IcomRigControl.UI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase, IAsyncDisposable
{
    private readonly Transceiver _transceiver;
    public Transceiver TransceiverInstance => _transceiver;

    private readonly ActivityLogger _logger;
    private readonly EmmcomBridge _emmcomBridge;
    private readonly HttpClient _emmcomHttpClient = new();
    private readonly SettingsService _settingsService;
    private readonly QsoLogger _qsoLogger;
    private readonly IAudioPlayer _audioPlayer = OperatingSystem.IsWindows()
        ? new NAudioPlayer()
        : new MacAudioPlayer();

    private RadioInfoUdpBroadcaster? _radioInfoBroadcaster;
    private ContactUdpListener? _contactListener;
    private ICallsignLookupSource? _callsignLookupSource;
    private LotwBridge? _lotwBridge;
    private HrdSqliteBridge? _hrdBridge;
    private AprsBeaconService? _aprsBeaconService;
    private PeriodicBeaconScheduler? _beaconScheduler;
    private AppSettings _currentSettings = new();

    [ObservableProperty]
    private bool _isAutoBeaconRunning;

    [ObservableProperty]
    private bool _isLogging;

    [ObservableProperty]
    private string _loggingStatus = "Not logging";

    [ObservableProperty]
    private bool _isEmmcomRunning;

    [ObservableProperty]
    private string _emmcomStatus = "EMMCOM: not connected";

    [ObservableProperty]
    private string _integrationsStatus = "Integrations: not started";

    [ObservableProperty]
    private string _frequencyDisplay = "---.---.---";

    [ObservableProperty]
    private string _mode = "---";

    [ObservableProperty]
    private bool _pttActive;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _sMeterDisplay = "S0";

    [ObservableProperty]
    private double _rfPowerPercent;

    [ObservableProperty]
    private double _swrRatio = 1.0;

    [ObservableProperty]
    private double _alcLevel;

    [ObservableProperty]
    private double _supplyVoltage;

    [ObservableProperty]
    private double _currentDraw;

    [ObservableProperty]
    private string _statusMessage = "Not connected";

    public List<string> AvailableModes { get; } = new() { "LSB", "USB", "AM", "CW", "FM" };

    [ObservableProperty]
    private string _frequencyInput = "14074000";

    [ObservableProperty]
    private string _emmcomUrlInput = "http://localhost:9000/api/rigstatus";

    [ObservableProperty]
    private string _aprsBeaconStatus = "APRS: not configured (see Settings)";

    [ObservableProperty]
    private bool _isSendingBeacon;

    /// Waterfall frequency axis labels (Phase 7). Each entry has a formatted
    /// frequency string and a 0.0-1.0 fraction the UI multiplies by the
    /// waterfall's actual rendered width to position it.
    public ObservableCollection<WaterfallAxisLabelViewModel> AxisLabels { get; } = new();

    public MainWindowViewModel()
    {
        var docsFolder = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "IcomRigControl");

        _settingsService = new SettingsService(System.IO.Path.Combine(docsFolder, "settings.json"));
        var settings = _settingsService.Load();

        // Phase 9: choose the real transport based on saved connection mode.
        // Demo (default) needs no hardware/network; Serial connects to a
        // local USB radio; Remote connects to a CivTcpServer over TCP,
        // possibly reachable via LAN, VPN, or 44Net/AMPRNet.
        ICivTransport transport = settings.ConnectionMode switch
        {
            "Serial" when !string.IsNullOrWhiteSpace(settings.SerialPortName) =>
                new SerialCivTransport(settings.SerialPortName),
            "Remote" when !string.IsNullOrWhiteSpace(settings.RemoteHost) =>
                new TcpCivTransport(settings.RemoteHost, settings.RemotePort, settings.RemoteAuthToken),
            _ => new DemoCivTransport()
        };

        _transceiver = new Transceiver(transport, RadioModel.IC7300);

        _transceiver.FrequencyChanged += (_, hz) =>
        {
            FrequencyDisplay = FormatFrequency(hz);
            UpdateAxisLabels();
        };

        _transceiver.ModeChanged += (_, mode) =>
            Mode = mode;

        _transceiver.PttChanged += (_, active) =>
            PttActive = active;

        _transceiver.MeterUpdated += (_, snapshot) =>
        {
            SMeterDisplay = snapshot.SMeterS >= 9 ? $"S9+{(int)(snapshot.SMeterDbm + 73 - 54)}dB" : $"S{snapshot.SMeterS}";
            RfPowerPercent = snapshot.RfPowerPercent;
            SwrRatio = snapshot.SwrRatio;
            AlcLevel = snapshot.AlcLevel;
            SupplyVoltage = snapshot.SupplyVoltage;
            CurrentDraw = snapshot.CurrentDraw;
        };

        _logger = new ActivityLogger(_transceiver, System.IO.Path.Combine(docsFolder, "Logs"));
        _emmcomBridge = new EmmcomBridge(_transceiver, _emmcomHttpClient, EmmcomUrlInput);
        _qsoLogger = new QsoLogger(_transceiver, System.IO.Path.Combine(docsFolder, "Logs"));
        _aprsBeaconService = new AprsBeaconService(_transceiver, _audioPlayer);
        _beaconScheduler = new PeriodicBeaconScheduler(SendBeacon);

        ApplySettings(settings);

        _ = ConnectAsync();
    }

    /// Recomputes the waterfall's frequency axis labels based on the
    /// Transceiver's current frequency and span. Called whenever frequency
    /// changes, and once after the scope starts.
    private void UpdateAxisLabels()
    {
        var labels = WaterfallFrequencyMapper.GenerateAxisLabels(
            _transceiver.FrequencyHz, _transceiver.CurrentSpanHz, labelCount: 5);

        AxisLabels.Clear();
        foreach (var label in labels)
        {
            AxisLabels.Add(new WaterfallAxisLabelViewModel(
                WaterfallFrequencyMapper.FormatFrequencyLabel(label.FrequencyHz),
                label.PixelX));
        }
    }

    /// Called by the waterfall control when the user clicks a point on it.
    /// clickFraction is 0.0 (left edge) to 1.0 (right edge) of the rendered
    /// waterfall width. Computes the corresponding frequency and tunes there.
    [RelayCommand]
    private async Task TuneToWaterfallPosition(double clickFraction)
    {
        if (!IsConnected) return;

        int pixelX = (int)(clickFraction * 474); // 475 data points, 0-474
        long targetFreq = WaterfallFrequencyMapper.PixelToFrequency(
            pixelX, dataPointCount: 475, _transceiver.FrequencyHz, _transceiver.CurrentSpanHz);

        await _transceiver.SetFrequencyAsync(targetFreq);
    }

    /// Toggles automatic periodic beaconing on/off, using the interval
    /// configured in Settings (AprsBeaconIntervalMinutes). See CLAUDE.md
    /// Phase 10.
    [RelayCommand]
    private void ToggleAutoBeacon()
    {
        if (_beaconScheduler == null) return;

        if (IsAutoBeaconRunning)
        {
            _beaconScheduler.Stop();
            IsAutoBeaconRunning = false;
            AprsBeaconStatus = string.IsNullOrWhiteSpace(_currentSettings.AprsCallsign)
                ? "APRS: no callsign configured (see Settings)"
                : $"APRS: ready ({_currentSettings.AprsCallsign}-{_currentSettings.AprsSsid})";
        }
        else
        {
            if (_currentSettings.AprsBeaconIntervalMinutes <= 0)
            {
                AprsBeaconStatus = "APRS: auto-beacon interval is 0 (set one in Settings)";
                return;
            }

            _beaconScheduler.Start(TimeSpan.FromMinutes(_currentSettings.AprsBeaconIntervalMinutes));
            IsAutoBeaconRunning = true;
            AprsBeaconStatus = $"APRS: auto-beaconing every {_currentSettings.AprsBeaconIntervalMinutes} min";
        }
    }

    /// Sends one APRS beacon using the currently saved AprsX settings. Keys
    /// PTT, plays the AFSK audio through the configured device, and always
    /// releases PTT afterward (see AprsBeaconService for the safety
    /// guarantee). See CLAUDE.md Phase 10.
    [RelayCommand]
    private async Task SendBeacon()
    {
        if (_aprsBeaconService == null) return;

        if (string.IsNullOrWhiteSpace(_currentSettings.AprsCallsign))
        {
            AprsBeaconStatus = "APRS: no callsign configured (see Settings)";
            return;
        }

        IsSendingBeacon = true;
        AprsBeaconStatus = "APRS: sending beacon...";

        try
        {
            // Always append live frequency/mode to whatever comment is
            // configured, so a listening station sees both the operator's
            // own note AND current activity, rather than one replacing
            // the other.
            string freqModeSuffix = AprsPositionFormatter.FormatFrequencyBeaconComment(_transceiver.FrequencyHz, Mode);
            string comment = string.IsNullOrWhiteSpace(_currentSettings.AprsComment)
                ? freqModeSuffix
                : $"{_currentSettings.AprsComment} {freqModeSuffix}";

            string? deviceName = string.IsNullOrWhiteSpace(_currentSettings.AudioOutputDeviceName)
                ? null
                : _currentSettings.AudioOutputDeviceName;

            await _aprsBeaconService.SendBeaconAsync(
                callsign: _currentSettings.AprsCallsign,
                ssid: _currentSettings.AprsSsid,
                latitude: _currentSettings.AprsLatitude,
                longitude: _currentSettings.AprsLongitude,
                symbolTable: _currentSettings.AprsSymbolTable,
                symbolCode: _currentSettings.AprsSymbolCode,
                comment: comment,
                profile: AfskProfile.Hf300Baud,
                audioDeviceName: deviceName);

            AprsBeaconStatus = $"APRS: beacon sent at {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            AprsBeaconStatus = $"APRS: error sending beacon ({ex.Message})";
        }
        finally
        {
            IsSendingBeacon = false;
        }
    }

    /// Reads the saved AppSettings and configures the real services for
    /// callsign lookup, LoTW, HRD, and N1MM/WSJT-X UDP integration accordingly.
    /// This is the piece that connects the Settings window to actual runtime
    /// behavior — previously the Settings UI only saved values that nothing
    /// consumed. See CLAUDE.md Phase 8 "remaining" note.
    private void ApplySettings(AppSettings settings)
    {
        _currentSettings = settings;

        try
        {
            // Callsign lookup source selection (Phase 8c)
            _callsignLookupSource = settings.CallsignLookupSource switch
            {
                "QRZ" when !string.IsNullOrWhiteSpace(settings.QrzUsername) =>
                    new QrzLookupSource(new HttpClient(), settings.QrzUsername, settings.QrzPassword),
                "HamQTH" when !string.IsNullOrWhiteSpace(settings.HamQthUsername) =>
                    new HamQthLookupSource(new HttpClient(), settings.HamQthUsername, settings.HamQthPassword),
                _ => new CallookLookupSource(new HttpClient())
            };

            // LoTW (Phase 8d) — only configured if a TQSL path was provided
            if (!string.IsNullOrWhiteSpace(settings.TqslExecutablePath))
            {
                var tqslRunner = new TqslProcessRunner(settings.TqslExecutablePath);
                _lotwBridge = new LotwBridge(tqslRunner, new HttpClient());
            }

            // HRD Logbook direct-write bridge (Phase 8e, Layer 3) — only if enabled
            if (settings.HrdBridgeEnabled && !string.IsNullOrWhiteSpace(settings.HrdDatabasePath))
            {
                _hrdBridge = new HrdSqliteBridge(settings.HrdDatabasePath);
            }

            // N1MM/WSJT-X/HRD UDP integration (Phase 8f)
            if (settings.N1mmSendEnabled)
            {
                _radioInfoBroadcaster = new RadioInfoUdpBroadcaster(_transceiver);
                foreach (var (ip, port) in settings.N1mmDestinations)
                {
                    _radioInfoBroadcaster.AddDestination(ip, port);
                }
                _radioInfoBroadcaster.Start();
            }

            if (settings.N1mmReceiveEnabled)
            {
                _contactListener = new ContactUdpListener(_qsoLogger, settings.ContactListenPort);
                _contactListener.Start();
            }

            AprsBeaconStatus = string.IsNullOrWhiteSpace(settings.AprsCallsign)
                ? "APRS: no callsign configured (see Settings)"
                : $"APRS: ready ({settings.AprsCallsign}-{settings.AprsSsid})";

            var activeIntegrations = new List<string>();
            if (settings.ConnectionMode == "Remote") activeIntegrations.Add($"Remote radio ({settings.RemoteHost}:{settings.RemotePort})");
            if (settings.ConnectionMode == "Serial") activeIntegrations.Add($"Serial radio ({settings.SerialPortName})");
            if (settings.N1mmSendEnabled) activeIntegrations.Add("N1MM send");
            if (settings.N1mmReceiveEnabled) activeIntegrations.Add("N1MM receive");
            if (_hrdBridge != null) activeIntegrations.Add("HRD bridge");
            if (_lotwBridge != null) activeIntegrations.Add("LoTW ready");

            IntegrationsStatus = activeIntegrations.Count > 0
                ? $"Integrations: {string.Join(", ", activeIntegrations)}"
                : "Integrations: none enabled (see Settings)";
        }
        catch (Exception ex)
        {
            // Never let a settings misconfiguration prevent the app from starting —
            // per the never-crash pattern used throughout this project.
            IntegrationsStatus = $"Integrations: error applying settings ({ex.Message})";
        }
    }

    [RelayCommand]
    private async Task SetMode(string mode)
    {
        if (!IsConnected) return;
        await _transceiver.SetModeAsync(mode);
    }

    [RelayCommand]
    private async Task SetFrequency()
    {
        if (!IsConnected) return;
        if (long.TryParse(FrequencyInput, out long hz) && hz > 0)
        {
            await _transceiver.SetFrequencyAsync(hz);
        }
        else
        {
            StatusMessage = "Invalid frequency — digits only, e.g. 14074000";
        }
    }

    [RelayCommand]
    private async Task TogglePtt()
    {
        if (!IsConnected) return;
        await _transceiver.SetPttAsync(!PttActive);
    }

    [RelayCommand]
    private void OpenMemoryEditor()
    {
        var editorViewModel = new MemoryEditorViewModel(_transceiver);
        var editorWindow = new Views.MemoryEditorWindow
        {
            DataContext = editorViewModel
        };
        editorWindow.Show();
    }

    [RelayCommand]
    private void OpenQsoLogger()
    {
        var loggerViewModel = new QsoLoggerViewModel(_qsoLogger, _callsignLookupSource, _lotwBridge);
        var loggerWindow = new Views.QsoLoggerWindow
        {
            DataContext = loggerViewModel
        };
        loggerWindow.Show();
    }

    [RelayCommand]
    private void OpenSettings()
    {
        var settingsViewModel = new SettingsViewModel(_settingsService);
        var settingsWindow = new Views.SettingsWindow
        {
            DataContext = settingsViewModel
        };
        settingsWindow.Closed += (_, _) =>
        {
            // Re-apply integration settings when the Settings window closes.
            // Note: connection mode (Demo/Serial/Remote) requires a full app
            // restart to take effect, since it determines the Transceiver's
            // transport at construction time.
            _contactListener?.Stop();
            _radioInfoBroadcaster?.Stop();
            ApplySettings(_settingsService.Load());
        };
        settingsWindow.Show();
    }

    [RelayCommand]
    private void ToggleLogging()
    {
        try
        {
            if (IsLogging)
            {
                _logger.Stop();
                IsLogging = false;
                LoggingStatus = "Not logging";
            }
            else
            {
                _logger.Start();
                IsLogging = true;
                LoggingStatus = $"Logging to {System.IO.Path.GetFileName(_logger.CurrentFilePath)}";
            }
        }
        catch (Exception ex)
        {
            LoggingStatus = $"Logging error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ToggleEmmcom()
    {
        try
        {
            if (IsEmmcomRunning)
            {
                _emmcomBridge.Stop();
                IsEmmcomRunning = false;
                EmmcomStatus = "EMMCOM: not connected";
            }
            else
            {
                _emmcomBridge.Start();
                IsEmmcomRunning = true;
                EmmcomStatus = $"EMMCOM: pushing to {EmmcomUrlInput}";
            }
        }
        catch (Exception ex)
        {
            EmmcomStatus = $"EMMCOM error: {ex.Message}";
        }
    }

    private async Task ConnectAsync()
    {
        try
        {
            await _transceiver.ConnectAsync();
            IsConnected = _transceiver.IsConnected;
            StatusMessage = IsConnected ? "Connected" : "Connection failed";

            if (IsConnected)
            {
                await _transceiver.SetFrequencyAsync(14_074_000);
                _transceiver.StartPolling(TimeSpan.FromMilliseconds(500));
                await _transceiver.StartScopeAsync(TimeSpan.FromMilliseconds(300));
                UpdateAxisLabels();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    private static string FormatFrequency(long hz)
    {
        // 14074000 -> "14.074.000"
        var s = hz.ToString("D9");
        return $"{s[0..3]}.{s[3..6]}.{s[6..9]}".TrimStart('0', '.');
    }

    public async ValueTask DisposeAsync()
    {
        _contactListener?.Stop();
        _radioInfoBroadcaster?.Stop();
        await _transceiver.DisposeAsync();
    }
}

/// One frequency axis label for the waterfall display.
public record WaterfallAxisLabelViewModel(string Text, double PixelFraction);
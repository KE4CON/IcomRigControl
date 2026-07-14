using System;
using System.Collections.Generic;
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

    [ObservableProperty]
    private bool _isLogging;

    [ObservableProperty]
    private string _loggingStatus = "Not logging";

    [ObservableProperty]
    private bool _isEmmcomRunning;

    [ObservableProperty]
    private string _emmcomStatus = "EMMCOM: not connected";

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

    public MainWindowViewModel()
    {
        // For now: FakeCivTransport so the UI is fully demoable without hardware.
        // Swap to SerialCivTransport("/dev/tty.usbserial-XXXX") once the radio is connected.
        var transport = new DemoCivTransport();
        _transceiver = new Transceiver(transport, RadioModel.IC7300);

        _transceiver.FrequencyChanged += (_, hz) =>
            FrequencyDisplay = FormatFrequency(hz);

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

        _logger = new ActivityLogger(_transceiver, System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "IcomRigControl", "Logs"));

        _emmcomBridge = new EmmcomBridge(_transceiver, _emmcomHttpClient, EmmcomUrlInput);

        _ = ConnectAsync();
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
            StatusMessage = IsConnected ? "Connected (demo mode)" : "Connection failed";

            if (IsConnected)
            {
                await _transceiver.SetFrequencyAsync(14_074_000);
                _transceiver.StartPolling(TimeSpan.FromMilliseconds(500));
                await _transceiver.StartScopeAsync(TimeSpan.FromMilliseconds(300));
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
        await _transceiver.DisposeAsync();
    }
}
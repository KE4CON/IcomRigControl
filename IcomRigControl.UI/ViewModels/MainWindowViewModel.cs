using System;
using CommunityToolkit.Mvvm.ComponentModel;
using IcomRigControl.CivEngine;
using IcomRigControl.RigModel;
using IcomRigControl.UI.Demo;
namespace IcomRigControl.UI.ViewModels;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.Input;

public partial class MainWindowViewModel : ViewModelBase, IAsyncDisposable
{
    private readonly Transceiver _transceiver;
    public Transceiver TransceiverInstance => _transceiver;

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

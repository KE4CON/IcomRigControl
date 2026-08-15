using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IcomRigControl.RigModel;
using IcomRigControl.Services;

namespace IcomRigControl.UI.ViewModels;

/// <summary>
/// Remote Audio client (Phase 12). Connects to the headless server's UDP audio
/// port, plays the radio's RX audio, and — while Transmit is engaged — keys the
/// radio's PTT over the Phase 9 CI-V link and streams the operator's mic to the
/// radio. RX always plays; TX only streams while transmitting.
/// </summary>
public partial class RemoteAudioViewModel : ViewModelBase, IAsyncDisposable
{
    private const string DefaultDeviceLabel = "(default)";

    private readonly Transceiver _transceiver;
    private readonly SettingsService _settingsService;
    private RemoteAudioLink? _link;

    public List<string> CaptureDevices { get; }

    [ObservableProperty] private string _serverHost;
    [ObservableProperty] private int _audioPort;
    [ObservableProperty] private string _selectedCaptureDevice;
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private bool _isTransmitting;
    [ObservableProperty] private string _status = "Not connected. Connect to the radio's headless server audio port.";

    public RemoteAudioViewModel(Transceiver transceiver, SettingsService settingsService)
    {
        _transceiver = transceiver;
        _settingsService = settingsService;

        var settings = _settingsService.Load();
        _serverHost = settings.RemoteHost;
        _audioPort = settings.RemoteAudioPort;

        CaptureDevices = new List<string> { DefaultDeviceLabel };
        try { CaptureDevices.AddRange(AudioDevices.CreateCapture().GetAvailableDevices()); } catch { }

        _selectedCaptureDevice = CaptureDevices.Contains(settings.RemoteAudioCaptureDevice)
            ? settings.RemoteAudioCaptureDevice
            : DefaultDeviceLabel;
    }

    [RelayCommand]
    private void Connect()
    {
        if (string.IsNullOrWhiteSpace(ServerHost)) { Status = "Enter the server host (same as the Phase 9 remote host)."; return; }

        // Persist for next time.
        var settings = _settingsService.Load();
        settings.RemoteHost = ServerHost;
        settings.RemoteAudioPort = AudioPort;
        settings.RemoteAudioCaptureDevice = SelectedCaptureDevice == DefaultDeviceLabel ? "" : SelectedCaptureDevice;
        _settingsService.Save(settings);

        try
        {
            _link = new RemoteAudioLink(AudioDevices.CreateCapture(), AudioDevices.CreateStreamOutput())
            {
                SendEnabled = false // mic off until transmitting
            };
            string? mic = SelectedCaptureDevice == DefaultDeviceLabel ? null : SelectedCaptureDevice;
            // localPort 0 = ephemeral; the server learns our address from keepalives.
            _link.Start(localPort: 0, remoteHost: ServerHost, remotePort: AudioPort, captureDeviceName: mic);
            IsConnected = true;
            Status = $"Connected to {ServerHost}:{AudioPort}. Listening.";
        }
        catch (Exception ex)
        {
            Status = $"Audio connect failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task Disconnect()
    {
        if (IsTransmitting) IsTransmitting = false; // releases PTT via the handler
        await DisposeLinkAsync();
        IsConnected = false;
        Status = "Disconnected.";
    }

    // Bound TwoWay to the Transmit toggle; keying the radio and gating the mic.
    partial void OnIsTransmittingChanged(bool value) => _ = ApplyTransmitAsync(value);

    private async Task ApplyTransmitAsync(bool transmit)
    {
        if (_link is null || !IsConnected)
        {
            if (transmit) IsTransmitting = false; // can't transmit while not connected
            return;
        }
        try
        {
            if (transmit)
            {
                await _transceiver.SetPttAsync(true); // key first...
                _link.SendEnabled = true;             // ...then open the mic
                Status = "TRANSMITTING — mic streaming to the radio.";
            }
            else
            {
                _link.SendEnabled = false;            // close the mic first...
                await _transceiver.SetPttAsync(false); // ...then unkey
                Status = "Receiving.";
            }
        }
        catch (Exception ex)
        {
            Status = $"PTT error: {ex.Message}";
        }
    }

    private async Task DisposeLinkAsync()
    {
        if (_link is not null)
        {
            await _link.DisposeAsync();
            _link = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        // Safety: never leave the radio keyed on teardown.
        if (IsTransmitting)
        {
            try { await _transceiver.SetPttAsync(false); } catch { }
        }
        await DisposeLinkAsync();
    }
}

using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IcomRigControl.RigModel;
using IcomRigControl.Services;

namespace IcomRigControl.UI.ViewModels;

/// <summary>
/// "Phone / Tablet Remote" window: starts the WebRemoteServer against the running
/// Transceiver and shows the http:// addresses to open in a phone or tablet browser
/// on the same network. Keep this window open while you operate remotely — closing
/// it stops the server. See CLAUDE.md web remote.
/// </summary>
public partial class WebRemoteViewModel : ViewModelBase, IAsyncDisposable
{
    private readonly Transceiver _transceiver;
    private readonly SettingsService _settingsService;
    private WebRemoteServer? _server;

    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private int _port;
    [ObservableProperty] private string _token = "";
    [ObservableProperty] private bool _useHttps;
    [ObservableProperty] private string _status = "Off. Start the server, then open one of the addresses on your phone or tablet (same Wi-Fi).";

    public ObservableCollection<string> Urls { get; } = new();

    public WebRemoteViewModel(Transceiver transceiver, SettingsService settingsService)
    {
        _transceiver = transceiver;
        _settingsService = settingsService;
        var s = settingsService.Load();
        _port = s.WebRemotePort;
        _token = s.WebRemoteToken;
        _useHttps = s.WebRemoteUseHttps;
    }

    [RelayCommand]
    private void Start()
    {
        if (_server is not null) return;
        try
        {
            var s = _settingsService.Load();
            s.WebRemotePort = Port;
            s.WebRemoteToken = Token;
            s.WebRemoteUseHttps = UseHttps;
            _settingsService.Save(s);

            _server = new WebRemoteServer(_transceiver, string.IsNullOrWhiteSpace(Token) ? null : Token, Port,
                captureFactory: AudioDevices.CreateCapture,        // phone can listen to RX audio
                txOutputFactory: AudioDevices.CreateStreamOutput,  // phone can transmit (mic -> radio)
                useHttps: UseHttps);
            _server.Start();

            Urls.Clear();
            foreach (string url in WebRemoteServer.GetLanUrls(Port, _server.Scheme)) Urls.Add(url);
            if (Urls.Count == 0) Urls.Add($"{_server.Scheme}://<this computer's IP>:{Port}");

            IsRunning = true;
            string tokenNote = string.IsNullOrWhiteSpace(Token)
                ? "No token set — trusted networks only."
                : "Enter the access token when the phone asks.";
            Status = UseHttps
                ? $"Running (HTTPS). Open an address below; your browser will warn about the certificate — tap Advanced → proceed. {tokenNote} Talk (mic) works over HTTPS."
                : $"Running. Open an address below. {tokenNote} (Enable HTTPS if you want to Talk from a phone.)";
        }
        catch (Exception ex)
        {
            Status = $"Could not start: {ex.Message}";
            _server = null;
        }
    }

    [RelayCommand]
    private void Stop()
    {
        _server?.Stop();
        _server = null;
        IsRunning = false;
        Status = "Stopped.";
    }

    public async ValueTask DisposeAsync()
    {
        if (_server is not null) await _server.DisposeAsync();
        _server = null;
    }
}

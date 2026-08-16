using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IcomRigControl.CivEngine;
using IcomRigControl.RigModel;
using IcomRigControl.Services;

namespace IcomRigControl.UI.ViewModels;

/// <summary>
/// RTTY reader window: decodes Baudot RTTY off the radio's RX audio
/// (RttyDecodeService) into a scrolling text area. A Reverse toggle swaps
/// mark/space for the wrong-sideband case and takes effect live. Non-modal, so the
/// main dashboard stays visible alongside it. See CLAUDE.md RTTY decode.
/// </summary>
public partial class RttyDecodeViewModel : ViewModelBase, IAsyncDisposable
{
    private const int MaxTextLength = 4000;

    private readonly SettingsService _settingsService;
    private readonly AudioTransmitter _transmitter;
    private readonly LiveRttyTransmitter _liveTransmitter;
    private RttyDecodeService? _service;
    private int _sentLen; // how much of SendText has already been streamed in live mode

    [ObservableProperty] private bool _isReceiving;
    [ObservableProperty] private string _decodedText = "";
    [ObservableProperty] private bool _reverse;
    [ObservableProperty] private string _sendText = "";
    [ObservableProperty] private bool _liveTx;
    [ObservableProperty] private string _status = "Set the radio to RTTY (or USB with 2125/2295 Hz tones) and Start. If you get garble, try Reverse.";

    public RttyDecodeViewModel(SettingsService settingsService, Transceiver transceiver, IAudioPlayer audioPlayer)
    {
        _settingsService = settingsService;
        _transmitter = new AudioTransmitter(transceiver, audioPlayer);
        _liveTransmitter = new LiveRttyTransmitter(transceiver, AudioDevices.CreateStreamOutput());
        _reverse = settingsService.Load().RttyReverse;
    }

    // Live mode: hold the transmitter keyed and stream characters as they're typed.
    partial void OnLiveTxChanged(bool value) => _ = SetLiveTxAsync(value);

    private async Task SetLiveTxAsync(bool on)
    {
        if (on)
        {
            string? device = _settingsService.Load().AudioOutputDeviceName is { Length: > 0 } d ? d : null;
            bool started = await _liveTransmitter.StartAsync(device);
            if (!started) { LiveTx = false; Status = "Can't transmit — TX is inhibited."; return; }
            _sentLen = SendText.Length;
            Status = "Live RTTY TX ON — the radio is keyed; type to send. Toggle off to stop.";
        }
        else
        {
            await _liveTransmitter.StopAsync();
            Status = "Live RTTY TX off.";
        }
    }

    partial void OnSendTextChanged(string value)
    {
        if (!LiveTx) return;
        if (value.Length > _sentLen)
        {
            string added = value[_sentLen..];
            _liveTransmitter.Enqueue(added);
            DecodedText += added.ToUpperInvariant();
        }
        _sentLen = value.Length; // (backspace can't un-send already-transmitted characters)
    }

    [RelayCommand]
    private void StartReceive()
    {
        if (_service is not null) return;
        try
        {
            var settings = _settingsService.Load();
            settings.RttyReverse = Reverse;
            _settingsService.Save(settings);

            string? device = string.IsNullOrWhiteSpace(settings.RemoteAudioCaptureDevice)
                ? null : settings.RemoteAudioCaptureDevice;

            _service = new RttyDecodeService(AudioDevices.CreateCapture(), RttyProfile.Hf45Baud, reverse: Reverse);
            _service.TextDecoded += OnTextDecoded;
            _service.Start(device);
            IsReceiving = true;
            Status = "Decoding RTTY.";
        }
        catch (Exception ex)
        {
            Status = $"Could not start: {ex.Message}";
        }
    }

    [RelayCommand]
    private void StopReceive()
    {
        StopService();
        IsReceiving = false;
        Status = "Stopped.";
    }

    [RelayCommand]
    private void Clear() => DecodedText = "";

    /// Modulates the typed text to RTTY audio and transmits it (keys PTT, plays,
    /// unkeys — honoring TX-inhibit). The sent text is echoed into the window.
    [RelayCommand]
    private async Task Send()
    {
        string text = SendText.Trim();
        if (string.IsNullOrEmpty(text)) return;
        try
        {
            var settings = _settingsService.Load();
            string? device = string.IsNullOrWhiteSpace(settings.AudioOutputDeviceName) ? null : settings.AudioOutputDeviceName;

            // "\r\n" idle diddle + message; RttyProfile defaults to 45.45 baud / 170 Hz.
            float[] audio = RttyModulator.Modulate(text + "\r\n", RttyProfile.Hf45Baud);
            Status = "Transmitting RTTY…";
            bool sent = await _transmitter.TransmitAsync(audio, 44100, device);
            if (sent)
            {
                DecodedText += (DecodedText.Length > 0 ? "\n" : "") + ">> " + text.ToUpperInvariant() + "\n";
                SendText = "";
                Status = "Sent.";
            }
            else
            {
                Status = "Busy transmitting — try again in a moment.";
            }
        }
        catch (Exception ex)
        {
            Status = $"Send error: {ex.Message}";
        }
    }

    /// Writes the decoded text to a timestamped .txt file in Documents/IcomRigControl,
    /// ready to open and print. Returns the path in the status line.
    [RelayCommand]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(DecodedText)) { Status = "Nothing decoded yet to save."; return; }
        try
        {
            string path = DecodeLog.Save("RTTY", DecodedText);
            Status = $"Saved to {path} — open it to print.";
        }
        catch (Exception ex)
        {
            Status = $"Save failed: {ex.Message}";
        }
    }

    // Flip polarity live when the toggle changes (CommunityToolkit generates this hook).
    partial void OnReverseChanged(bool value)
    {
        _service?.SetReverse(value);
        var settings = _settingsService.Load();
        settings.RttyReverse = value;
        _settingsService.Save(settings);
    }

    private void OnTextDecoded(object? sender, string text) => Dispatcher.UIThread.Post(() =>
    {
        string combined = DecodedText + text;
        if (combined.Length > MaxTextLength) combined = combined[^MaxTextLength..];
        DecodedText = combined;
    });

    private void StopService()
    {
        if (_service is not null)
        {
            _service.TextDecoded -= OnTextDecoded;
            _service.Stop();
            _service = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        StopService();
        await _liveTransmitter.StopAsync(); // never leave the radio keyed
    }
}

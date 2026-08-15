using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IcomRigControl.CivEngine;
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
    private RttyDecodeService? _service;

    [ObservableProperty] private bool _isReceiving;
    [ObservableProperty] private string _decodedText = "";
    [ObservableProperty] private bool _reverse;
    [ObservableProperty] private string _status = "Set the radio to RTTY (or USB with 2125/2295 Hz tones) and Start. If you get garble, try Reverse.";

    public RttyDecodeViewModel(SettingsService settingsService)
    {
        _settingsService = settingsService;
        _reverse = settingsService.Load().RttyReverse;
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

    public ValueTask DisposeAsync()
    {
        StopService();
        return ValueTask.CompletedTask;
    }
}

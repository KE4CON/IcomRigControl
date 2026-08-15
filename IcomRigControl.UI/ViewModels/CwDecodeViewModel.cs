using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IcomRigControl.RigModel;
using IcomRigControl.Services;

namespace IcomRigControl.UI.ViewModels;

/// <summary>
/// CW reader window: decodes Morse off the radio's RX audio (CwDecodeService) into a
/// scrolling text area, shows the live sender speed, and provides a "Zero Beat"
/// button that measures how far the received tone is from the CW pitch and nudges
/// the radio (over CI-V) to tune exactly onto the station. Non-modal, so the main
/// dashboard stays visible alongside it. See CLAUDE.md CW decode / zero beat.
/// </summary>
public partial class CwDecodeViewModel : ViewModelBase, IAsyncDisposable
{
    private const int MaxTextLength = 4000;

    private readonly Transceiver _transceiver;
    private readonly SettingsService _settingsService;
    private CwDecodeService? _service;

    [ObservableProperty] private bool _isReceiving;
    [ObservableProperty] private string _decodedText = "";
    [ObservableProperty] private double _pitchHz = 600;
    [ObservableProperty] private int _wpm;
    [ObservableProperty] private string _tuningHint = "Waiting for a signal…";
    [ObservableProperty] private bool _reverseZeroBeat;
    [ObservableProperty] private string _status = "Set the radio to CW, put a signal in the passband, and Start. Match Pitch to the radio's CW Pitch setting.";

    public CwDecodeViewModel(Transceiver transceiver, SettingsService settingsService)
    {
        _transceiver = transceiver;
        _settingsService = settingsService;
        var settings = settingsService.Load();
        _pitchHz = settings.CwPitchHz;
        _reverseZeroBeat = settings.CwZeroBeatReverse;
    }

    [RelayCommand]
    private void StartReceive()
    {
        if (_service is not null) return;
        try
        {
            // Persist the chosen pitch / direction for next time.
            var settings = _settingsService.Load();
            settings.CwPitchHz = PitchHz;
            settings.CwZeroBeatReverse = ReverseZeroBeat;
            _settingsService.Save(settings);

            string? device = string.IsNullOrWhiteSpace(settings.RemoteAudioCaptureDevice)
                ? null : settings.RemoteAudioCaptureDevice;

            _service = new CwDecodeService(AudioDevices.CreateCapture(), PitchHz);
            _service.TextDecoded += OnTextDecoded;
            _service.WpmChanged += OnWpmChanged;
            _service.ToneMeasured += OnToneMeasured;
            _service.Start(device);
            IsReceiving = true;
            Status = "Decoding CW.";
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

    /// Tunes the radio so the received tone lands on the CW pitch (zero beat).
    [RelayCommand]
    private async Task ZeroBeat()
    {
        if (_service?.LastToneHz is not { } tone)
        {
            Status = "No signal to zero-beat yet — wait for a tone.";
            return;
        }

        double error = tone - PitchHz;            // + = tone too high
        if (Math.Abs(error) < 3)
        {
            Status = "Already on frequency (zero beat).";
            return;
        }

        // CW-Normal: raise the VFO to lower the tone. Reverse flips the sign.
        long correction = (long)Math.Round(ReverseZeroBeat ? -error : error);
        long newFreq = _transceiver.FrequencyHz + correction;
        try
        {
            await _transceiver.SetFrequencyAsync(newFreq);
            Status = $"Zero beat: tuned {(correction >= 0 ? "+" : "")}{correction} Hz (tone was {tone:F0} Hz).";
        }
        catch (Exception ex)
        {
            Status = $"Zero-beat tune failed: {ex.Message}";
        }
    }

    private void OnTextDecoded(object? sender, string text) => Dispatcher.UIThread.Post(() =>
    {
        string combined = DecodedText + text;
        if (combined.Length > MaxTextLength) combined = combined[^MaxTextLength..];
        DecodedText = combined;
    });

    private void OnWpmChanged(object? sender, double wpm) =>
        Dispatcher.UIThread.Post(() => Wpm = (int)Math.Round(wpm));

    private void OnToneMeasured(object? sender, CwToneReading reading) => Dispatcher.UIThread.Post(() =>
    {
        double e = reading.ErrorHz;
        TuningHint = Math.Abs(e) < 10
            ? $"On frequency ✓  ({reading.ToneHz:F0} Hz)"
            : e > 0
                ? $"Signal {e:F0} Hz high — Zero Beat to tune"
                : $"Signal {-e:F0} Hz low — Zero Beat to tune";
    });

    private void StopService()
    {
        if (_service is not null)
        {
            _service.TextDecoded -= OnTextDecoded;
            _service.WpmChanged -= OnWpmChanged;
            _service.ToneMeasured -= OnToneMeasured;
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

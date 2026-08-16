using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IcomRigControl.Services;

namespace IcomRigControl.UI.ViewModels;

/// <summary>
/// Band DVR window: monitors the radio's receive audio into a rolling buffer so you
/// can instantly replay the last 30/60 seconds ("who was that? — rewind"), and can
/// record continuously to a WAV file. Non-modal, alongside the main dashboard.
/// See CLAUDE.md Band DVR.
/// </summary>
public partial class BandDvrViewModel : ViewModelBase, IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly IAudioPlayer _player = AudioDevices.CreatePlayer();
    private BandRecorder? _recorder;

    [ObservableProperty] private bool _isMonitoring;
    [ObservableProperty] private bool _isRecording;
    [ObservableProperty] private string _status = "Off. Start monitoring to buffer the last minute of receive audio, then replay or record it.";

    private static string RecordingsDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "IcomRigControl", "Recordings");

    public BandDvrViewModel(SettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    [RelayCommand]
    private void StartMonitor()
    {
        if (_recorder is not null) return;
        try
        {
            string? device = _settingsService.Load().RemoteAudioCaptureDevice;
            _recorder = new BandRecorder(AudioDevices.CreateCapture(), sampleRateHz: 44100, rollingSeconds: 60);
            _recorder.Start(string.IsNullOrWhiteSpace(device) ? null : device);
            IsMonitoring = true;
            Status = "Monitoring. The last 60 seconds are always available to replay.";
        }
        catch (Exception ex)
        {
            Status = $"Could not start: {ex.Message}";
            _recorder = null;
        }
    }

    [RelayCommand]
    private void StopMonitor()
    {
        _recorder?.Stop();
        _recorder = null;
        IsMonitoring = false;
        IsRecording = false;
        Status = "Stopped.";
    }

    [RelayCommand]
    private void ToggleRecord()
    {
        if (_recorder is null) { Status = "Start monitoring first."; return; }
        try
        {
            if (!IsRecording)
            {
                string path = _recorder.StartRecording(RecordingsDir);
                IsRecording = true;
                Status = $"Recording to {path}";
            }
            else
            {
                _recorder.StopRecording();
                IsRecording = false;
                Status = "Recording saved.";
            }
        }
        catch (Exception ex)
        {
            Status = $"Record error: {ex.Message}";
        }
    }

    [RelayCommand]
    private Task ReplayLast30() => ReplayAsync(30);

    [RelayCommand]
    private Task ReplayLast60() => ReplayAsync(60);

    private async Task ReplayAsync(int seconds)
    {
        if (_recorder is null) { Status = "Start monitoring first."; return; }
        short[] pcm = _recorder.GetRewind(seconds);
        if (pcm.Length == 0) { Status = "Nothing buffered yet."; return; }
        var audio = new float[pcm.Length];
        for (int i = 0; i < pcm.Length; i++) audio[i] = pcm[i] / 32768f;
        Status = $"Replaying the last {Math.Min(seconds, pcm.Length / _recorder.SampleRate)} s…";
        try { await _player.PlayAsync(audio, _recorder.SampleRate); Status = "Replay done."; }
        catch (Exception ex) { Status = $"Replay error: {ex.Message}"; }
    }

    [RelayCommand]
    private void OpenRecordingsFolder()
    {
        try
        {
            Directory.CreateDirectory(RecordingsDir);
            Process.Start(new ProcessStartInfo(RecordingsDir) { UseShellExecute = true });
        }
        catch (Exception ex) { Status = $"Could not open folder: {ex.Message}"; }
    }

    public void Dispose()
    {
        _recorder?.Dispose();
        _recorder = null;
    }
}

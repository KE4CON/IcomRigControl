using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IcomRigControl.Services;

namespace IcomRigControl.UI.ViewModels;

/// <summary>
/// Band DVR window: controls the app-wide BandRecorder (shared, so per-QSO audio and
/// monitoring survive closing this window) — instant replay of the last minute,
/// record-to-WAV, and a recordings manager (play, delete, total size) so the disk
/// doesn't fill up. See CLAUDE.md Band DVR.
/// </summary>
public partial class BandDvrViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private readonly BandRecorder _recorder;
    private readonly ScopeRecorder _scopeRecorder;
    private readonly IAudioPlayer _player = AudioDevices.CreatePlayer();

    /// Raised when the user asks to replay the waterfall history — the view renders
    /// the frames into its waterfall control (kept in the view to stay MVVM-clean).
    public event Action<IReadOnlyList<int[]>>? WaterfallReplayRequested;

    [ObservableProperty] private bool _isMonitoring;
    [ObservableProperty] private bool _isRecording;
    [ObservableProperty] private string _totalSize = "";
    [ObservableProperty] private string _status = "Monitor keeps the last 60 seconds ready to replay. While monitoring, each contact you log gets its audio saved too.";

    public ObservableCollection<RecordingRow> Recordings { get; } = new();

    public BandDvrViewModel(SettingsService settingsService, BandRecorder recorder, ScopeRecorder scopeRecorder)
    {
        _settingsService = settingsService;
        _recorder = recorder;
        _scopeRecorder = scopeRecorder;
        _isMonitoring = recorder.IsCapturing;
        _isRecording = recorder.IsRecording;
        RefreshRecordings();
    }

    /// Replays the buffered waterfall history into the view's waterfall control.
    [RelayCommand]
    private void ReplayWaterfall()
    {
        var frames = _scopeRecorder.GetFrames();
        if (frames.Count == 0) { Status = "No waterfall history yet — the scope needs to be running."; return; }
        WaterfallReplayRequested?.Invoke(frames);
        Status = $"Replayed {frames.Count} waterfall frames (most recent at the bottom).";
    }

    [RelayCommand]
    private void StartMonitor()
    {
        if (_recorder.IsCapturing) { IsMonitoring = true; return; }
        try
        {
            string? device = _settingsService.Load().RemoteAudioCaptureDevice;
            _recorder.Start(string.IsNullOrWhiteSpace(device) ? null : device);
            IsMonitoring = true;
            Status = "Monitoring. Replay the last minute, record, and per-QSO audio are all live.";
        }
        catch (Exception ex) { Status = $"Could not start: {ex.Message}"; }
    }

    [RelayCommand]
    private void StopMonitor()
    {
        _recorder.Stop();
        IsMonitoring = false;
        IsRecording = false;
        Status = "Stopped.";
    }

    [RelayCommand]
    private void ToggleRecord()
    {
        if (!_recorder.IsCapturing) { Status = "Start monitoring first."; return; }
        try
        {
            if (!IsRecording)
            {
                string path = _recorder.StartRecording(BandRecorder.RecordingsDir);
                IsRecording = true;
                Status = $"Recording to {Path.GetFileName(path)}";
            }
            else
            {
                _recorder.StopRecording();
                IsRecording = false;
                Status = "Recording saved.";
                RefreshRecordings();
            }
        }
        catch (Exception ex) { Status = $"Record error: {ex.Message}"; }
    }

    [RelayCommand]
    private Task ReplayLast30() => ReplayAsync(30);

    [RelayCommand]
    private Task ReplayLast60() => ReplayAsync(60);

    private async Task ReplayAsync(int seconds)
    {
        if (!_recorder.IsCapturing) { Status = "Start monitoring first."; return; }
        short[] pcm = _recorder.GetRewind(seconds);
        if (pcm.Length == 0) { Status = "Nothing buffered yet."; return; }
        var audio = new float[pcm.Length];
        for (int i = 0; i < pcm.Length; i++) audio[i] = pcm[i] / 32768f;
        Status = $"Replaying the last {Math.Min(seconds, pcm.Length / _recorder.SampleRate)} s…";
        try { await _player.PlayAsync(audio, _recorder.SampleRate); Status = "Replay done."; }
        catch (Exception ex) { Status = $"Replay error: {ex.Message}"; }
    }

    [RelayCommand]
    private void RefreshRecordings()
    {
        Recordings.Clear();
        long total = 0;
        foreach (string dir in new[] { BandRecorder.RecordingsDir, BandRecorder.QsoAudioDir })
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var fi in new DirectoryInfo(dir).GetFiles("*.wav").OrderByDescending(f => f.LastWriteTime))
            {
                Recordings.Add(new RecordingRow(fi.Name, fi.FullName, SizeText(fi.Length)));
                total += fi.Length;
            }
        }
        TotalSize = $"{Recordings.Count} file(s), {SizeText(total)} total";
    }

    [RelayCommand]
    private void PlayRecording(RecordingRow? row)
    {
        if (row is null) return;
        try { Process.Start(new ProcessStartInfo(row.Path) { UseShellExecute = true }); }
        catch (Exception ex) { Status = $"Could not play: {ex.Message}"; }
    }

    [RelayCommand]
    private void DeleteRecording(RecordingRow? row)
    {
        if (row is null) return;
        try { File.Delete(row.Path); Status = $"Deleted {row.Name}."; }
        catch (Exception ex) { Status = $"Delete failed: {ex.Message}"; }
        RefreshRecordings();
    }

    [RelayCommand]
    private void DeleteAllRecordings()
    {
        int deleted = 0;
        foreach (var row in Recordings.ToList())
        {
            try { File.Delete(row.Path); deleted++; } catch { }
        }
        Status = $"Deleted {deleted} file(s).";
        RefreshRecordings();
    }

    [RelayCommand]
    private void OpenRecordingsFolder()
    {
        try
        {
            Directory.CreateDirectory(BandRecorder.RecordingsDir);
            Process.Start(new ProcessStartInfo(BandRecorder.RecordingsDir) { UseShellExecute = true });
        }
        catch (Exception ex) { Status = $"Could not open folder: {ex.Message}"; }
    }

    private static string SizeText(long bytes) => bytes switch
    {
        >= 1_000_000 => $"{bytes / 1_000_000.0:F1} MB",
        >= 1_000 => $"{bytes / 1_000.0:F0} KB",
        _ => $"{bytes} B",
    };
}

public record RecordingRow(string Name, string Path, string SizeText);

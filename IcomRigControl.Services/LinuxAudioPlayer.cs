using System.Diagnostics;
using IcomRigControl.CivEngine;

namespace IcomRigControl.Services;

/// <summary>
/// Linux / Raspberry Pi audio playback via ALSA's `aplay` (shelled out to a temp
/// WAV, exactly like MacAudioPlayer does with afplay). This finally gives the app
/// a Linux/Pi audio player — previously IAudioPlayer was Windows+macOS only, so
/// the Phase 10 APRS beacon couldn't play on Linux. Now it can, AND this is the
/// TX-audio player for the Pi remote-audio server. See CLAUDE.md Phase 12.
/// </summary>
public class LinuxAudioPlayer : IAudioPlayer
{
    private Process? _process;

    public bool IsPlaying { get; private set; }

    /// aplay follows the ALSA default device; it doesn't enumerate outputs the
    /// way WASAPI does, so return a single placeholder (like MacAudioPlayer).
    public List<string> GetAvailableDevices() => new() { "System Default (aplay)" };

    public async Task PlayAsync(float[] samples, int sampleRateHz, string? deviceName = null)
    {
        string tempPath = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), $"icomrigcontrol_audio_{Guid.NewGuid()}.wav");
        try
        {
            WavFileWriter.WriteToFile(tempPath, samples, sampleRateHz);

            var startInfo = new ProcessStartInfo
            {
                FileName = "aplay",
                Arguments = $"-q \"{tempPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            _process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Failed to start aplay (is ALSA installed?).");

            IsPlaying = true;
            await _process.WaitForExitAsync();
            IsPlaying = false;
        }
        finally
        {
            if (System.IO.File.Exists(tempPath)) System.IO.File.Delete(tempPath);
        }
    }

    public void Stop()
    {
        try
        {
            if (_process is { HasExited: false }) _process.Kill();
        }
        catch { /* never let Stop throw */ }
        finally { IsPlaying = false; }
    }
}

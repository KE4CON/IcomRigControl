using System.Diagnostics;
using IcomRigControl.CivEngine;

namespace IcomRigControl.Services;

/// <summary>
/// Plays generated audio samples (e.g. AfskModulator's AFSK tones) on
/// macOS by writing them to a temporary WAV file (via WavFileWriter) and
/// shelling out to afplay, the built-in macOS command-line audio player --
/// the same "shell out to the OS's own tool" pattern already used for LoTW
/// signing via TqslProcessRunner, rather than binding to AVFoundation
/// directly. afplay has no device-selection capability of its own, so
/// playback always goes through whatever output device is currently
/// selected in System Settings > Sound -- a documented limitation, not a
/// bug. See CLAUDE.md Phase 10.
/// </summary>
public class MacAudioPlayer : IAudioPlayer
{
    private Process? _process;

    public bool IsPlaying { get; private set; }

    /// afplay cannot enumerate or select specific output devices, so this
    /// always returns a single placeholder entry rather than a real device
    /// list. Playback follows the system's current default output device.
    public List<string> GetAvailableDevices()
    {
        return new List<string> { "System Default (afplay)" };
    }

    public async Task PlayAsync(float[] samples, int sampleRateHz, string? deviceName = null)
    {
        string tempPath = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), $"icomrigcontrol_audio_{Guid.NewGuid()}.wav");

        try
        {
            WavFileWriter.WriteToFile(tempPath, samples, sampleRateHz);

            var startInfo = new ProcessStartInfo
            {
                FileName = "afplay",
                Arguments = $"\"{tempPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            _process = Process.Start(startInfo);
            if (_process == null)
            {
                throw new InvalidOperationException("Failed to start afplay process.");
            }

            IsPlaying = true;
            await _process.WaitForExitAsync();
            IsPlaying = false;
        }
        finally
        {
            // Always clean up the temp WAV file, even if afplay failed to
            // start or playback was interrupted.
            if (System.IO.File.Exists(tempPath))
            {
                System.IO.File.Delete(tempPath);
            }
        }
    }

    public void Stop()
    {
        try
        {
            if (_process != null && !_process.HasExited)
            {
                _process.Kill();
            }
        }
        catch
        {
            // Process may have already exited between the check and Kill()
            // -- never let Stop() throw, matching this project's
            // never-crash pattern for control operations.
        }
        finally
        {
            IsPlaying = false;
        }
    }
}

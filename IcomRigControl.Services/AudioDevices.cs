namespace IcomRigControl.Services;

/// <summary>
/// The single place that picks the right platform audio implementation, so every
/// construction site stays platform-correct (see CLAUDE.md's cross-platform
/// lesson — a hardcoded `new NAudioPlayer()` once crashed macOS). Windows uses
/// NAudio, macOS/Linux shell out to afplay/aplay/arecord, and the Pi is covered
/// by the Linux path.
/// </summary>
public static class AudioDevices
{
    public static IAudioPlayer CreatePlayer()
    {
        if (OperatingSystem.IsWindows()) return new NAudioPlayer();
        if (OperatingSystem.IsMacOS()) return new MacAudioPlayer();
        return new LinuxAudioPlayer(); // Linux / Raspberry Pi
    }

    public static IAudioCapture CreateCapture()
    {
        if (OperatingSystem.IsWindows()) return new NAudioCapture();
        if (OperatingSystem.IsLinux()) return new LinuxAudioCapture(); // incl. Raspberry Pi
        return new MacAudioCapture(); // macOS (stub for now)
    }
}

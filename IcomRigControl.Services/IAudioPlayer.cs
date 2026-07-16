namespace IcomRigControl.Services;

/// <summary>
/// Platform-agnostic audio playback abstraction, so callers (like
/// AprsBeaconService) don't depend on NAudio directly. Windows uses
/// NAudioPlayer; a future macOS implementation would use AVFoundation
/// behind this same interface. See CLAUDE.md Phase 10.
/// </summary>
public interface IAudioPlayer
{
    bool IsPlaying { get; }
    Task PlayAsync(float[] samples, int sampleRateHz, string? deviceName = null);
    void Stop();
    List<string> GetAvailableDevices();
}
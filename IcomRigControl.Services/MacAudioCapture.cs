namespace IcomRigControl.Services;

/// <summary>
/// macOS audio capture placeholder. macOS has no built-in command-line capture
/// tool equivalent to afplay (which is playback only), so this is a no-op stub
/// for now: a macOS client can LISTEN to remote RX audio (via MacAudioPlayer)
/// but cannot yet CAPTURE for TX. A real implementation would shell out to SoX
/// `rec`/ffmpeg or bind AVFoundation. See CLAUDE.md Phase 12 remaining work.
/// </summary>
public class MacAudioCapture : IAudioCapture
{
    public bool IsCapturing => false;

    // Never raised — capture isn't implemented on macOS yet.
#pragma warning disable CS0067
    public event EventHandler<short[]>? SamplesCaptured;
#pragma warning restore CS0067

    public List<string> GetAvailableDevices() =>
        new() { "(macOS capture not yet implemented)" };

    public void Start(int sampleRateHz, string? deviceName = null)
    {
        // No-op: capture on macOS is not implemented yet (documented). Doing
        // nothing rather than throwing keeps a macOS client usable for RX-only.
    }

    public void Stop() { }
}

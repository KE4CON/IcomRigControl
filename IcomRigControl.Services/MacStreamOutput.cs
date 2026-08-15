namespace IcomRigControl.Services;

/// <summary>
/// macOS continuous audio output placeholder. afplay is file-based and can't play
/// a live stream from stdin, and there's no other built-in CLI streamer, so this
/// is a no-op stub for now (a real version would use SoX `play` or CoreAudio).
/// A macOS client therefore can't yet play remote RX audio continuously — the
/// working paths are Windows and Linux/Pi. See CLAUDE.md Phase 12 remaining work.
/// </summary>
public class MacStreamOutput : IAudioStreamOutput
{
    public void Start(int sampleRateHz, string? deviceName = null) { }
    public void Write(short[] pcmFrame) { }
    public void Stop() { }
}

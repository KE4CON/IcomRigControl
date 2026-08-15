namespace IcomRigControl.Services;

/// <summary>
/// Platform-agnostic audio CAPTURE abstraction — the missing half of the audio
/// stack for Phase 12 remote audio (IAudioPlayer only plays). Captures 16-bit
/// mono PCM from an input device (the radio's USB audio for RX streaming, or the
/// operator's microphone for TX) and raises SamplesCaptured for each buffer.
///
/// Planned implementations, one per platform (like IAudioPlayer's
/// NAudioPlayer/MacAudioPlayer): NAudioCapture (Windows, WASAPI/WaveIn),
/// LinuxAudioCapture (Linux/Raspberry Pi via ALSA `arecord` — essential for the
/// headless Pi server at the radio), and a macOS implementation. Selection must
/// be platform-aware at EVERY construction site (see CLAUDE.md's cross-platform
/// lesson).
/// </summary>
public interface IAudioCapture
{
    bool IsCapturing { get; }

    /// Raised for each captured buffer of 16-bit mono PCM at the started rate.
    event EventHandler<short[]>? SamplesCaptured;

    /// Begin capturing at the given sample rate from the named device (null =
    /// system default input).
    void Start(int sampleRateHz, string? deviceName = null);

    void Stop();

    /// Available input (capture) device names.
    List<string> GetAvailableDevices();
}

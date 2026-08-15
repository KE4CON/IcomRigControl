namespace IcomRigControl.Services;

/// <summary>
/// Continuous audio OUTPUT for the Phase 12 remote-audio stream — distinct from
/// IAudioPlayer, which plays a whole clip once (fine for an APRS beacon, useless
/// for a live stream). This one is fed decoded PCM frames as they arrive and
/// plays them back gaplessly. Windows uses NAudio's BufferedWaveProvider; the Pi
/// pipes raw PCM to `aplay`'s stdin. See CLAUDE.md Phase 12 milestone 3.
/// </summary>
public interface IAudioStreamOutput
{
    /// Open the output at the given sample rate (16-bit mono). deviceName routes
    /// to a specific device (e.g. an ALSA "plughw:X,Y" for the radio on the Pi);
    /// null uses the system default.
    void Start(int sampleRateHz, string? deviceName = null);

    /// Queue one frame of 16-bit mono PCM for continuous play-out.
    void Write(short[] pcmFrame);

    void Stop();
}

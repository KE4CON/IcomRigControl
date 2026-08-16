using IcomRigControl.RigModel;

namespace IcomRigControl.Services;

/// <summary>
/// The safe primitive for transmitting generated audio (RTTY, and anything else that
/// keys the radio and plays a tone into its transmit-audio input): acquire a gate so
/// two sends can't overlap, key PTT (which itself honors TransmitInhibited), let the
/// radio settle, play the audio, then ALWAYS release PTT via try/finally — leaving the
/// transmitter stuck on is the real hazard this class exists to prevent. Mirrors the
/// APRS beacon's transmit discipline, generalized. See CLAUDE.md digital-mode TX.
/// </summary>
public sealed class AudioTransmitter
{
    private readonly Transceiver _transceiver;
    private readonly IAudioPlayer _player;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public AudioTransmitter(Transceiver transceiver, IAudioPlayer player)
    {
        _transceiver = transceiver;
        _player = player;
    }

    /// Keys the radio, plays <paramref name="audio"/>, and releases PTT. Returns false
    /// if another transmission is already in flight (the re-entrancy guard).
    public async Task<bool> TransmitAsync(float[] audio, int sampleRateHz,
        string? audioDeviceName = null, int pttSettleMilliseconds = 300)
    {
        if (!await _gate.WaitAsync(0)) return false;
        try
        {
            try
            {
                await _transceiver.SetPttAsync(true); // no-op if TransmitInhibited
                await Task.Delay(pttSettleMilliseconds);
                await _player.PlayAsync(audio, sampleRateHz, audioDeviceName);
            }
            finally
            {
                await _transceiver.SetPttAsync(false); // always release
            }
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }
}

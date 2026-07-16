using IcomRigControl.CivEngine;
using IcomRigControl.RigModel;

namespace IcomRigControl.Services;

/// <summary>
/// Orchestrates a complete HF APRS beacon transmission: build the APRS
/// position report and AX.25 frame, modulate it to AFSK audio, key PTT on,
/// play the audio through the selected device, then release PTT — always,
/// even if audio playback fails. This is the safety-critical piece that
/// ties Phase 10's protocol/audio work to actually transmitting: without
/// PTT keying, audio would just play into a receiving radio and never
/// actually go out over the air. See CLAUDE.md Phase 10.
/// </summary>
public class AprsBeaconService
{
    private readonly Transceiver _transceiver;
    private readonly IAudioPlayer _audioPlayer;

    public AprsBeaconService(Transceiver transceiver, IAudioPlayer audioPlayer)
    {
        _transceiver = transceiver;
        _audioPlayer = audioPlayer;
    }

    /// Sends one APRS beacon: keys PTT, plays the generated AFSK audio, and
    /// releases PTT afterward — guaranteed, via try/finally, even if
    /// something in between throws. Leaving PTT stuck on is a real safety
    /// issue (the radio would keep transmitting indefinitely), so this
    /// guarantee is the whole point of this class, not an afterthought.
    public async Task SendBeaconAsync(
        string callsign, int ssid,
        double latitude, double longitude,
        char symbolTable, char symbolCode, string comment,
        AfskProfile profile,
        int sampleRateHz = 44100,
        string? audioDeviceName = null,
        int pttSettleMilliseconds = 300)
    {
        string position = AprsPositionFormatter.FormatPosition(latitude, longitude, symbolTable, symbolCode, comment);

        byte[] frame = Ax25FrameBuilder.BuildUiFrame(
            sourceCallsign: callsign, sourceSsid: ssid,
            destinationCallsign: "APRS", destinationSsid: 0,
            infoField: position);

        float[] audio = AfskModulator.ModulateFrame(frame, profile, sampleRateHz);

        await _transceiver.SetPttAsync(true);
        try
        {
            // Give the radio a moment to actually key up and settle into
            // transmit before sending audio — sending audio the instant PTT
            // is requested can clip the very start of the packet on real
            // hardware (relay/PTT switching isn't instantaneous).
            await Task.Delay(pttSettleMilliseconds);

            await _audioPlayer.PlayAsync(audio, sampleRateHz, audioDeviceName);
        }
        finally
        {
            await _transceiver.SetPttAsync(false);
        }
    }
}
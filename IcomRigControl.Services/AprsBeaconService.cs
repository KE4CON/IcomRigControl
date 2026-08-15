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

    // Only one beacon may key the transmitter at a time. The manual "Send
    // Beacon" button and the periodic auto-beacon scheduler both call
    // SendBeaconAsync; this gate prevents them (or two rapid manual clicks)
    // from cross-keying the radio.
    private readonly SemaphoreSlim _transmitGate = new(1, 1);

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
    ///
    /// Returns true if the beacon was transmitted, or false if it was skipped
    /// because another beacon was already in flight (re-entrancy guard) —
    /// keying PTT a second time while the first beacon is still playing would
    /// cross-key the radio and corrupt the over-the-air packet.
    public async Task<bool> SendBeaconAsync(
        string callsign, int ssid,
        double latitude, double longitude,
        char symbolTable, char symbolCode, string comment,
        AfskProfile profile,
        int sampleRateHz = 44100,
        string? audioDeviceName = null,
        int pttSettleMilliseconds = 300)
    {
        // Non-blocking acquire: if a beacon is already transmitting, skip this
        // one rather than transmit on top of it.
        if (!await _transmitGate.WaitAsync(0))
            return false;

        try
        {
            string position = AprsPositionFormatter.FormatPosition(latitude, longitude, symbolTable, symbolCode, comment);

            byte[] frame = Ax25FrameBuilder.BuildUiFrame(
                sourceCallsign: callsign, sourceSsid: ssid,
                destinationCallsign: "APRS", destinationSsid: 0,
                infoField: position);

            float[] audio = AfskModulator.ModulateFrame(frame, profile, sampleRateHz);

            // Key-up is INSIDE the try so the finally's PTT release runs no
            // matter what fails after the radio is keyed — including an
            // exception from a PttChanged event handler fired by SetPttAsync.
            // Previously key-up sat above the try, so such a failure left the
            // transmitter stuck on with no key-down.
            try
            {
                await _transceiver.SetPttAsync(true);

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

            return true;
        }
        finally
        {
            _transmitGate.Release();
        }
    }
}
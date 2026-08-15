namespace IcomRigControl.CivEngine;

/// <summary>
/// A decoded APRS reception: the AX.25 frame and its parsed APRS content.
/// </summary>
public record AprsReception(Ax25Frame Frame, AprsPacket Packet);

/// <summary>
/// Decodes every APRS packet in an audio buffer, with bit-sync. Because a packet
/// can start at any sample, this sweeps the bit-timing phase across one bit period
/// (0 .. samplesPerBit-1) and decodes at each — any phase within a bit period maps
/// a packet onto correct bit boundaries, so a real off-air packet is found whatever
/// its arrival time. Only FCS-valid frames survive, deduped by source+info. This is
/// the simple, correct first-pass bit-sync; a transition-tracking DPLL would be more
/// efficient and more tolerant of timing drift (a live-hardware refinement).
/// See CLAUDE.md HF APRS.
/// </summary>
public static class AprsReceiver
{
    public static List<AprsReception> DecodePackets(float[] audio, AfskProfile profile, int sampleRateHz)
    {
        int samplesPerBit = (int)System.Math.Round(sampleRateHz / (double)profile.BaudRate);
        var seen = new HashSet<string>();
        var results = new List<AprsReception>();
        if (samplesPerBit <= 0) return results;

        for (int phase = 0; phase < samplesPerBit; phase++)
        {
            var bits = AfskDemodulator.Demodulate(audio, profile, sampleRateHz, phase);
            foreach (var frameBytes in HdlcDeframer.ExtractFrames(bits))
            {
                var frame = Ax25Decoder.Decode(frameBytes);
                if (frame is null) continue;

                string key = $"{frame.Source}-{frame.SourceSsid}|{frame.InfoField}";
                if (!seen.Add(key)) continue;

                results.Add(new AprsReception(frame, AprsParser.Parse(frame.InfoField)));
            }
        }
        return results;
    }

    /// Convenience overload for 16-bit PCM (e.g. from IAudioCapture).
    public static List<AprsReception> DecodePackets(short[] pcm, AfskProfile profile, int sampleRateHz)
    {
        var audio = new float[pcm.Length];
        for (int i = 0; i < pcm.Length; i++) audio[i] = pcm[i] / 32768f;
        return DecodePackets(audio, profile, sampleRateHz);
    }
}

namespace IcomRigControl.CivEngine;

/// <summary>
/// AFSK (Audio Frequency-Shift Keying) modulator: converts raw frame bytes
/// into playable audio samples, per the AX.25/APRS physical-layer chain:
/// bit-stuff the data (HDLC rule: insert a 0 after five consecutive 1s, to
/// keep the flag pattern 01111110 unambiguous), NRZI-encode the stuffed
/// bitstream (a '1' bit = no tone-transition, a '0' bit = transition), then
/// generate mark/space tones for each bit period. See CLAUDE.md Phase 10.
/// </summary>
public static class AfskModulator
{
    /// HDLC bit-stuffing: after five consecutive '1' bits, insert a '0'.
    /// This ensures the data stream never accidentally contains the flag
    /// pattern (six consecutive 1s), which marks frame boundaries.
    public static bool[] BitStuff(bool[] bits)
    {
        var result = new List<bool>();
        int consecutiveOnes = 0;

        foreach (bool bit in bits)
        {
            result.Add(bit);

            if (bit)
            {
                consecutiveOnes++;
                if (consecutiveOnes == 5)
                {
                    result.Add(false); // stuffed bit
                    consecutiveOnes = 0;
                }
            }
            else
            {
                consecutiveOnes = 0;
            }
        }

        return result.ToArray();
    }

    /// NRZI encoding: a '1' bit means "keep the current signal level" (no
    /// transition), a '0' bit means "flip the signal level" (transition).
    /// Starting level is conventionally high (true).
    public static bool[] NrziEncode(bool[] bits)
    {
        var result = new bool[bits.Length];
        bool currentLevel = true;

        for (int i = 0; i < bits.Length; i++)
        {
            if (!bits[i])
            {
                currentLevel = !currentLevel;
            }
            result[i] = currentLevel;
        }

        return result;
    }

    /// Generates one bit-period's worth of a sine wave tone at the given
    /// frequency, continuing from the given starting phase (so consecutive
    /// bit tones join without a phase discontinuity/click). Outputs the
    /// ending phase via the out parameter for the next call to continue from.
    public static float[] GenerateBitTone(double frequencyHz, int sampleRateHz, int baudRate, double phase, out double endPhase)
    {
        double samplesPerBitExact = sampleRateHz / (double)baudRate;
        int sampleCount = (int)Math.Round(samplesPerBitExact);
        var samples = new float[sampleCount];

        double phaseIncrement = 2 * Math.PI * frequencyHz / sampleRateHz;

        for (int i = 0; i < sampleCount; i++)
        {
            samples[i] = (float)Math.Sin(phase);
            phase += phaseIncrement;
        }

        endPhase = phase % (2 * Math.PI);
        return samples;
    }

    /// The HDLC flag 0x7E (01111110) as LSB-first bits. Flags delimit frames and
    /// are the ONLY place six consecutive 1s may appear — they are never bit-stuffed.
    private static readonly bool[] FlagBits = { false, true, true, true, true, true, true, false };

    /// Modulates raw frame bytes with bit-stuffing + NRZI + tones, but NO HDLC
    /// flags and NO FCS. Kept for the low-level tests; real on-air frames must use
    /// ModulateAx25Frame so a receiver can actually sync and validate them.
    public static float[] ModulateFrame(byte[] frameBytes, AfskProfile profile, int sampleRateHz)
    {
        var bits = BitStuff(BytesToLsbFirstBits(frameBytes));
        return TonesFromBits(NrziEncode(bits), profile, sampleRateHz);
    }

    /// Modulates a complete, DECODABLE AX.25 AFSK frame: append the FCS, bit-stuff
    /// (frame+FCS), wrap in HDLC flags (a preamble of flags for receiver sync, and
    /// a closing flag — flags are not stuffed), then NRZI-encode the whole stream
    /// and generate continuous-phase mark/space tones. This is the correct on-air
    /// chain; ModulateFrame above omits the flags/FCS and won't decode.
    public static float[] ModulateAx25Frame(byte[] frameBytes, AfskProfile profile, int sampleRateHz, int preambleFlagCount = 16)
    {
        var fcs = Ax25Fcs.ComputeBytes(frameBytes);
        var frameWithFcs = new byte[frameBytes.Length + 2];
        Array.Copy(frameBytes, frameWithFcs, frameBytes.Length);
        frameWithFcs[^2] = fcs[0];
        frameWithFcs[^1] = fcs[1];

        var stuffed = BitStuff(BytesToLsbFirstBits(frameWithFcs));

        var allBits = new List<bool>(preambleFlagCount * 8 + stuffed.Length + 8);
        for (int i = 0; i < preambleFlagCount; i++) allBits.AddRange(FlagBits);
        allBits.AddRange(stuffed);
        allBits.AddRange(FlagBits); // closing flag

        return TonesFromBits(NrziEncode(allBits.ToArray()), profile, sampleRateHz);
    }

    private static float[] TonesFromBits(bool[] nrziLevels, AfskProfile profile, int sampleRateHz)
    {
        var audio = new List<float>();
        double phase = 0;
        foreach (bool level in nrziLevels)
        {
            double freq = level ? profile.MarkFrequencyHz : profile.SpaceFrequencyHz;
            audio.AddRange(GenerateBitTone(freq, sampleRateHz, profile.BaudRate, phase, out phase));
        }
        return audio.ToArray();
    }

    private static bool[] BytesToLsbFirstBits(byte[] bytes)
    {
        var bits = new bool[bytes.Length * 8];
        for (int i = 0; i < bytes.Length; i++)
        {
            for (int b = 0; b < 8; b++)
            {
                // LSB-first, per AX.25's bit-transmission order.
                bits[i * 8 + b] = ((bytes[i] >> b) & 0x01) == 1;
            }
        }
        return bits;
    }
}
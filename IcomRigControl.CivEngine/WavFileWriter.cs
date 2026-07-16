namespace IcomRigControl.CivEngine;

/// <summary>
/// Writes float audio samples (-1.0 to 1.0 range) as a standard 16-bit PCM
/// mono WAV file, per the canonical RIFF/WAVE format. Used to save
/// AfskModulator's generated tones as a playable file — useful for
/// confirming a packet "sounds right" before attempting live playback
/// through a real audio device. See CLAUDE.md Phase 10.
/// </summary>
public static class WavFileWriter
{
    private const int BitsPerSample = 16;
    private const int Channels = 1; // mono

    public static byte[] WriteToBytes(float[] samples, int sampleRateHz)
    {
        int dataSize = samples.Length * (BitsPerSample / 8);
        int fileSize = 36 + dataSize;
        int byteRate = sampleRateHz * Channels * (BitsPerSample / 8);
        short blockAlign = (short)(Channels * (BitsPerSample / 8));

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        // RIFF chunk
        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(fileSize);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

        // fmt chunk
        writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16); // fmt chunk size for PCM
        writer.Write((short)1); // AudioFormat: 1 = PCM
        writer.Write((short)Channels);
        writer.Write(sampleRateHz);
        writer.Write(byteRate);
        writer.Write(blockAlign);
        writer.Write((short)BitsPerSample);

        // data chunk
        writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        writer.Write(dataSize);

        foreach (float sample in samples)
        {
            short scaled = FloatToInt16(sample);
            writer.Write(scaled);
        }

        return stream.ToArray();
    }

    public static void WriteToFile(string path, float[] samples, int sampleRateHz)
    {
        var bytes = WriteToBytes(samples, sampleRateHz);
        File.WriteAllBytes(path, bytes);
    }

    private static short FloatToInt16(float sample)
    {
        float clamped = Math.Clamp(sample, -1.0f, 1.0f);
        return clamped >= 0
            ? (short)(clamped * short.MaxValue)
            : (short)(clamped * -short.MinValue);
    }
}
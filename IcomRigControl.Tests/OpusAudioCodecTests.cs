using System;
using IcomRigControl.Services;
using Xunit;

namespace IcomRigControl.Tests;

public class OpusAudioCodecTests
{
    [Fact]
    public void FrameSize_MatchesSampleRateAndFrameLength()
    {
        var codec = new OpusAudioCodec(sampleRate: 16000, frameMilliseconds: 20);
        Assert.Equal(320, codec.FrameSize); // 16000 * 20 / 1000
    }

    [Fact]
    public void EncodeThenDecode_RoundTripsAToneFrame()
    {
        var codec = new OpusAudioCodec(sampleRate: 16000, frameMilliseconds: 20);

        // A 1 kHz sine wave frame.
        var pcm = new short[codec.FrameSize];
        for (int i = 0; i < pcm.Length; i++)
            pcm[i] = (short)(8000 * Math.Sin(2 * Math.PI * 1000 * i / codec.SampleRate));

        byte[] packet = codec.Encode(pcm);
        Assert.NotEmpty(packet);
        Assert.True(packet.Length < pcm.Length * 2, "Opus packet should be smaller than raw PCM.");

        short[] decoded = codec.Decode(packet);

        Assert.Equal(codec.FrameSize, decoded.Length);
        // Opus is lossy, so we can't compare sample-for-sample — but a decoded
        // tone must carry real energy, not silence.
        double rms = Rms(decoded);
        Assert.True(rms > 500, $"Decoded frame RMS was {rms:F0}; expected a non-silent tone.");
    }

    [Fact]
    public void Decode_NullPacket_ProducesAConcealmentFrameOfTheRightLength()
    {
        var codec = new OpusAudioCodec();
        short[] concealed = codec.Decode(null);
        Assert.Equal(codec.FrameSize, concealed.Length);
    }

    private static double Rms(short[] samples)
    {
        double sum = 0;
        foreach (short s in samples) sum += (double)s * s;
        return Math.Sqrt(sum / samples.Length);
    }
}

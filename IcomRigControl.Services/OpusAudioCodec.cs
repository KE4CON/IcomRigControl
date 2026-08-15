using Concentus.Enums;
using Concentus.Structs;

namespace IcomRigControl.Services;

/// <summary>
/// Wraps the Concentus Opus encoder/decoder for the remote-audio stream (Phase
/// 12). Concentus is a pure-C# Opus implementation, so this works on every
/// platform the app targets — Windows, macOS, Linux, and the Raspberry Pi — with
/// no native library. Mono, VoIP-tuned. Operates on fixed frames (default 20 ms).
/// </summary>
public class OpusAudioCodec
{
    /// Sample rate in Hz. Opus supports 8000/12000/16000/24000/48000. 16 kHz
    /// (wideband) is plenty for SSB/CW/AM radio audio and keeps bitrate low.
    public int SampleRate { get; }

    /// Samples in one frame (SampleRate * frameMs / 1000). Encode/Decode work one
    /// frame at a time — the caller must feed exactly this many samples.
    public int FrameSize { get; }

    private readonly OpusEncoder _encoder;
    private readonly OpusDecoder _decoder;
    private readonly byte[] _encodeBuffer = new byte[4000];

    public OpusAudioCodec(int sampleRate = 16000, int frameMilliseconds = 20)
    {
        SampleRate = sampleRate;
        FrameSize = sampleRate * frameMilliseconds / 1000;
        _encoder = new OpusEncoder(sampleRate, 1, OpusApplication.OPUS_APPLICATION_VOIP);
        _decoder = new OpusDecoder(sampleRate, 1);
    }

    /// Encode exactly one frame of 16-bit PCM (FrameSize samples) to an Opus packet.
    public byte[] Encode(short[] pcmFrame)
    {
        int len = _encoder.Encode(pcmFrame, 0, FrameSize, _encodeBuffer, 0, _encodeBuffer.Length);
        var packet = new byte[len];
        System.Array.Copy(_encodeBuffer, packet, len);
        return packet;
    }

    /// Decode an Opus packet back to one frame of 16-bit PCM (FrameSize samples).
    /// Pass null to conceal a lost packet (Opus PLC fills the gap).
    public short[] Decode(byte[]? packet)
    {
        var pcm = new short[FrameSize];
        if (packet is null)
            _decoder.Decode(null, 0, 0, pcm, 0, FrameSize, false);
        else
            _decoder.Decode(packet, 0, packet.Length, pcm, 0, FrameSize, false);
        return pcm;
    }
}

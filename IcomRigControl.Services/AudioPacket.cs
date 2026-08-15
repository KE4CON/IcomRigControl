namespace IcomRigControl.Services;

/// <summary>
/// One UDP audio packet for the Phase 12 remote-audio stream: a small RTP-style
/// header (16-bit sequence + 32-bit sample timestamp, big-endian) followed by an
/// Opus payload. Sequence detects loss/reordering; timestamp drives play-out.
/// </summary>
public readonly record struct AudioPacket(ushort Sequence, uint Timestamp, byte[] Payload)
{
    public const int HeaderSize = 6;

    /// Serialize to bytes ready to send over UDP.
    public byte[] Serialize()
    {
        var buf = new byte[HeaderSize + Payload.Length];
        buf[0] = (byte)(Sequence >> 8);
        buf[1] = (byte)Sequence;
        buf[2] = (byte)(Timestamp >> 24);
        buf[3] = (byte)(Timestamp >> 16);
        buf[4] = (byte)(Timestamp >> 8);
        buf[5] = (byte)Timestamp;
        System.Array.Copy(Payload, 0, buf, HeaderSize, Payload.Length);
        return buf;
    }

    /// Parse a received datagram, or null if it's too short to be valid.
    public static AudioPacket? Parse(System.ReadOnlySpan<byte> data)
    {
        if (data.Length < HeaderSize) return null;

        ushort seq = (ushort)((data[0] << 8) | data[1]);
        uint ts = ((uint)data[2] << 24) | ((uint)data[3] << 16) | ((uint)data[4] << 8) | data[5];
        return new AudioPacket(seq, ts, data.Slice(HeaderSize).ToArray());
    }
}

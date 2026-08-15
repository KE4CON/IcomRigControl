using System.Text;
using IcomRigControl.Services;
using Xunit;

namespace IcomRigControl.Tests;

public class AudioPacketTests
{
    [Fact]
    public void Serialize_ThenParse_RoundTrips()
    {
        var payload = Encoding.ASCII.GetBytes("opusdata");
        var packet = new AudioPacket(0x1234, 0xDEADBEEF, payload);

        var parsed = AudioPacket.Parse(packet.Serialize());

        Assert.NotNull(parsed);
        Assert.Equal((ushort)0x1234, parsed!.Value.Sequence);
        Assert.Equal(0xDEADBEEFu, parsed.Value.Timestamp);
        Assert.Equal(payload, parsed.Value.Payload);
    }

    [Fact]
    public void Parse_TooShort_ReturnsNull()
    {
        Assert.Null(AudioPacket.Parse(new byte[] { 0x00, 0x01, 0x02 }));
    }

    [Fact]
    public void Header_IsSixBytes()
    {
        var packet = new AudioPacket(1, 2, new byte[10]);
        Assert.Equal(AudioPacket.HeaderSize + 10, packet.Serialize().Length);
    }
}

public class JitterBufferTests
{
    private static byte[] Frame(int n) => new[] { (byte)n };

    [Fact]
    public void Primes_ThenReleasesInOrder()
    {
        var jb = new JitterBuffer(targetDepth: 3);

        jb.Add(1, Frame(1));
        jb.Add(2, Frame(2));
        Assert.Null(jb.GetNext()); // still priming (2 < 3)

        jb.Add(3, Frame(3));
        Assert.Equal(Frame(1), jb.GetNext());
        Assert.Equal(Frame(2), jb.GetNext());
        Assert.Equal(Frame(3), jb.GetNext());
        Assert.Null(jb.GetNext()); // underrun
    }

    [Fact]
    public void ReordersOutOfOrderArrivals()
    {
        var jb = new JitterBuffer(targetDepth: 3);
        jb.Add(3, Frame(3));
        jb.Add(1, Frame(1));
        jb.Add(2, Frame(2));

        Assert.Equal(Frame(1), jb.GetNext());
        Assert.Equal(Frame(2), jb.GetNext());
        Assert.Equal(Frame(3), jb.GetNext());
    }

    [Fact]
    public void MissingFrame_IsConcealedAndSequenceAdvances()
    {
        var jb = new JitterBuffer(targetDepth: 3);
        jb.Add(1, Frame(1));
        jb.Add(2, Frame(2));
        jb.Add(4, Frame(4)); // 3 is lost

        Assert.Equal(Frame(1), jb.GetNext());
        Assert.Equal(Frame(2), jb.GetNext());
        Assert.Null(jb.GetNext());        // frame 3 missing -> conceal
        Assert.Equal(Frame(4), jb.GetNext());
    }
}

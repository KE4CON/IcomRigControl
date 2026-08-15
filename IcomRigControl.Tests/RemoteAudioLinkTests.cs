using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using IcomRigControl.Services;
using Xunit;

namespace IcomRigControl.Tests;

public class FrameAccumulatorTests
{
    [Fact]
    public void EmitsCompleteFrames_AndKeepsTheRemainder()
    {
        var acc = new FrameAccumulator(320);

        var first = acc.Add(new short[500]);      // 500 -> 1 frame, 180 left
        Assert.Single(first);
        Assert.Equal(320, first[0].Length);

        var second = acc.Add(new short[200]);     // 180+200=380 -> 1 frame, 60 left
        Assert.Single(second);

        var third = acc.Add(new short[100]);      // 60+100=160 -> no full frame
        Assert.Empty(third);

        var fourth = acc.Add(new short[500]);     // 160+500=660 -> 2 frames, 20 left
        Assert.Equal(2, fourth.Count);
    }
}

public class RemoteAudioLinkTests
{
    [Fact]
    public async Task AudioFlowsEndToEnd_OverUdp_ThroughOpusAndJitter()
    {
        int portA = FreeUdpPort();
        int portB = FreeUdpPort();

        var capA = new FakeCapture();
        var outB = new RecordingStreamOutput();

        await using var linkA = new RemoteAudioLink(capA, new RecordingStreamOutput());
        await using var linkB = new RemoteAudioLink(new FakeCapture(), outB);

        linkB.Start(portB, "127.0.0.1", portA);
        linkA.Start(portA, "127.0.0.1", portB);

        // Push a 1 kHz tone into A's capture: enough frames to prime B's jitter
        // buffer and keep the play-out loop fed.
        const int sampleRate = 16000;
        const int frameSize = 320;
        var tone = new short[frameSize * 25];
        for (int i = 0; i < tone.Length; i++)
            tone[i] = (short)(8000 * Math.Sin(2 * Math.PI * 1000 * i / sampleRate));
        capA.Push(tone);

        // The tone must arrive at B's output after traversing
        // capture -> Opus -> UDP -> jitter buffer -> Opus decode -> output.
        var deadline = DateTime.UtcNow.AddSeconds(10);
        bool gotTone = false;
        while (DateTime.UtcNow < deadline && !gotTone)
        {
            foreach (var frame in outB.Snapshot())
            {
                Assert.Equal(frameSize, frame.Length); // every played frame is one codec frame
                if (Rms(frame) > 500) { gotTone = true; break; }
            }
            if (!gotTone) await Task.Delay(50);
        }

        Assert.True(gotTone, "Expected the tone to reach the far end's audio output.");
    }

    [Fact]
    public async Task ServerMode_LearnsClientFromKeepalive_ThenStreamsRxAudioToIt()
    {
        int serverPort = FreeUdpPort();

        var serverCapture = new FakeCapture();
        var clientOutput = new RecordingStreamOutput();

        // Server always streams the "radio" audio; client is receive-only (mic off).
        await using var server = new RemoteAudioLink(serverCapture, new RecordingStreamOutput()) { SendEnabled = true };
        await using var client = new RemoteAudioLink(new FakeCapture(), clientOutput) { SendEnabled = false };

        server.StartServer(serverPort);
        client.Start(localPort: 0, remoteHost: "127.0.0.1", remotePort: serverPort);

        // Give a client keepalive time to reach the server so it learns the address.
        await Task.Delay(1500);

        serverCapture.Push(MakeTone(320 * 25));

        var deadline = DateTime.UtcNow.AddSeconds(10);
        bool gotTone = false;
        while (DateTime.UtcNow < deadline && !gotTone)
        {
            foreach (var frame in clientOutput.Snapshot())
                if (Rms(frame) > 500) { gotTone = true; break; }
            if (!gotTone) await Task.Delay(50);
        }

        Assert.True(gotTone, "Server should stream RX audio to the client it learned via keepalive.");
    }

    private static short[] MakeTone(int length)
    {
        var tone = new short[length];
        for (int i = 0; i < length; i++)
            tone[i] = (short)(8000 * Math.Sin(2 * Math.PI * 1000 * i / 16000));
        return tone;
    }

    private static int FreeUdpPort()
    {
        using var u = new UdpClient(0);
        return ((IPEndPoint)u.Client.LocalEndPoint!).Port;
    }

    private static double Rms(short[] s)
    {
        double sum = 0;
        foreach (short v in s) sum += (double)v * v;
        return Math.Sqrt(sum / s.Length);
    }

    private sealed class FakeCapture : IAudioCapture
    {
        public bool IsCapturing { get; private set; }
        public event EventHandler<short[]>? SamplesCaptured;
        public void Start(int sampleRateHz, string? deviceName = null) => IsCapturing = true;
        public void Stop() => IsCapturing = false;
        public List<string> GetAvailableDevices() => new();
        public void Push(short[] samples) => SamplesCaptured?.Invoke(this, samples);
    }

    private sealed class RecordingStreamOutput : IAudioStreamOutput
    {
        private readonly List<short[]> _frames = new();
        public void Start(int sampleRateHz, string? deviceName = null) { }
        public void Write(short[] pcmFrame) { lock (_frames) _frames.Add(pcmFrame); }
        public void Stop() { }
        public List<short[]> Snapshot() { lock (_frames) return new List<short[]>(_frames); }
    }
}

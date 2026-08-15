using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using IcomRigControl.CivEngine;
using IcomRigControl.Services;
using Xunit;

namespace IcomRigControl.Tests;

public class AprsReceiveServiceTests
{
    [Fact]
    public async Task DecodesAPushedPacket_AndRaisesPacketReceived()
    {
        var capture = new FakeCapture();
        var service = new AprsReceiveService(capture, AfskProfile.Hf300Baud, sampleRateHz: 44100, processIntervalMs: 150);

        var got = new TaskCompletionSource<AprsReception>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.PacketReceived += (_, rx) => got.TrySetResult(rx);

        service.Start();

        // Push a real modulated APRS packet as PCM, with lead/trail silence.
        var frame = Ax25FrameBuilder.BuildUiFrame("KE4CON", 9, "APRS", 0, "!4903.50N/07201.75W-mobile hf");
        float[] audio = AfskModulator.ModulateAx25Frame(frame, AfskProfile.Hf300Baud, 44100);
        var pcm = new short[600 + audio.Length + 600];
        for (int i = 0; i < audio.Length; i++) pcm[600 + i] = (short)(audio[i] * 32767);
        capture.Push(pcm);

        var completed = await Task.WhenAny(got.Task, Task.Delay(TimeSpan.FromSeconds(10)));
        Assert.True(completed == got.Task, "The receive service should decode and raise the pushed packet.");

        var reception = await got.Task;
        Assert.Equal("KE4CON", reception.Frame.Source);
        Assert.Equal(AprsPacketType.Position, reception.Packet.Type);

        service.Stop();
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
}

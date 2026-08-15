using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using IcomRigControl.CivEngine;
using IcomRigControl.Services;
using Xunit;

namespace IcomRigControl.Tests;

public class RttyDecodeServiceTests
{
    [Fact]
    public async Task DecodesPushedRtty_AcrossChunks()
    {
        var capture = new FakeCapture();
        var service = new RttyDecodeService(capture, RttyProfile.Hf45Baud, sampleRateHz: 44100);

        var text = new System.Text.StringBuilder();
        var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.TextDecoded += (_, s) =>
        {
            lock (text) { text.Append(s); if (text.ToString().Contains("KE4CON")) done.TrySetResult(true); }
        };

        service.Start();

        const string message = "CQ DE KE4CON";
        float[] audio = RttyModulator.Modulate(message, RttyProfile.Hf45Baud, 44100);
        var pcm = new short[audio.Length];
        for (int i = 0; i < audio.Length; i++) pcm[i] = (short)(audio[i] * 32767);

        // Push in several chunks to exercise the streaming/tail handling.
        int chunk = 4096;
        for (int off = 0; off < pcm.Length; off += chunk)
        {
            int len = Math.Min(chunk, pcm.Length - off);
            var part = new short[len];
            Array.Copy(pcm, off, part, 0, len);
            capture.Push(part);
        }

        var completed = await Task.WhenAny(done.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        service.Stop();
        Assert.True(completed == done.Task, $"Expected to decode the message; got '{text}'");
        Assert.Contains(message, text.ToString().Trim());
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

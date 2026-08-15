using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using IcomRigControl.CivEngine;
using IcomRigControl.Services;
using Xunit;

namespace IcomRigControl.Tests;

public class CwDecodeServiceTests
{
    [Fact]
    public async Task DecodesPushedCw_AndMeasuresTone()
    {
        var capture = new FakeCapture();
        var service = new CwDecodeService(capture, pitchHz: 600, sampleRateHz: 44100);

        var text = new System.Text.StringBuilder();
        service.TextDecoded += (_, s) => text.Append(s);

        var tone = new TaskCompletionSource<CwToneReading>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.ToneMeasured += (_, r) => tone.TrySetResult(r);

        service.Start();

        // Push a real CW transmission as PCM, with lead/trail silence.
        float[] audio = CwModulator.Modulate("CQ DE KE4CON", wpm: 20, pitchHz: 600, sampleRateHz: 44100);
        var pcm = new short[22050 + audio.Length + 22050];
        for (int i = 0; i < audio.Length; i++) pcm[22050 + i] = (short)(audio[i] * 32767);
        capture.Push(pcm);

        // Tone measurement should fire with the ~600 Hz tone.
        var completed = await Task.WhenAny(tone.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.True(completed == tone.Task, "A tone reading should be raised for the pushed CW.");
        Assert.True(Math.Abs(tone.Task.Result.ToneHz - 600) < 20, $"tone {tone.Task.Result.ToneHz} ~ 600");

        service.Stop(); // flushes the final character

        Assert.Contains("CQ DE KE4CON", text.ToString().Trim());
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

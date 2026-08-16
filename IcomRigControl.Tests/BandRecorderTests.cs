using System;
using System.Collections.Generic;
using System.IO;
using IcomRigControl.Services;
using Xunit;

namespace IcomRigControl.Tests;

public class WavWriterTests
{
    [Fact]
    public void WritesValidWav_ThatRoundTrips()
    {
        string path = Path.Combine(Path.GetTempPath(), $"irc_wav_{Guid.NewGuid():N}.wav");
        var samples = new short[8000];
        for (int i = 0; i < samples.Length; i++) samples[i] = (short)(i % 500 - 250);
        try
        {
            WavWriter.WriteFile(path, samples, 8000);

            byte[] file = File.ReadAllBytes(path);
            Assert.True(file.Length == 44 + samples.Length * 2, "header (44) + PCM data");
            Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(file, 0, 4));
            Assert.Equal("WAVE", System.Text.Encoding.ASCII.GetString(file, 8, 4));
            Assert.Equal("data", System.Text.Encoding.ASCII.GetString(file, 36, 4));
            int dataLen = BitConverter.ToInt32(file, 40);
            Assert.Equal(samples.Length * 2, dataLen);

            // First sample round-trips.
            short first = BitConverter.ToInt16(file, 44);
            Assert.Equal(samples[0], first);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}

public class BandRecorderTests
{
    [Fact]
    public void Rewind_ReturnsMostRecentSeconds()
    {
        var capture = new FakeCapture();
        // capture==store so no downsampling; capacity = 2s * 100 = 200.
        var rec = new BandRecorder(capture, captureRateHz: 100, storeRateHz: 100, rollingSeconds: 2);
        rec.Start();

        // Push 300 samples (0..299); ring holds the last 200 (100..299).
        var buf = new short[300];
        for (int i = 0; i < buf.Length; i++) buf[i] = (short)i;
        capture.Push(buf);

        short[] last1s = rec.GetRewind(1);   // last 100 samples
        Assert.Equal(100, last1s.Length);
        Assert.Equal(200, last1s[0]);        // oldest of the last 100
        Assert.Equal(299, last1s[^1]);       // newest

        short[] all = rec.GetRewind(10);     // more than buffered -> all 200
        Assert.Equal(200, all.Length);
        Assert.Equal(100, all[0]);
        rec.Stop();
    }

    [Fact]
    public void Records_PushedAudio_ToAWavFile()
    {
        var capture = new FakeCapture();
        var rec = new BandRecorder(capture, captureRateHz: 8000, storeRateHz: 8000, rollingSeconds: 5);
        string dir = Path.Combine(Path.GetTempPath(), $"irc_dvr_{Guid.NewGuid():N}");
        rec.Start();
        string path = rec.StartRecording(dir);
        try
        {
            var buf = new short[8000];
            for (int i = 0; i < buf.Length; i++) buf[i] = (short)(i % 300);
            capture.Push(buf);
            rec.StopRecording();
            rec.Stop();

            Assert.True(File.Exists(path));
            var file = File.ReadAllBytes(path);
            Assert.Equal(44 + buf.Length * 2, file.Length);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void Downsamples_CaptureRateToStoreRate()
    {
        var capture = new FakeCapture();
        // Capture 8000, store 2000 -> keep every 4th sample.
        var rec = new BandRecorder(capture, captureRateHz: 8000, storeRateHz: 2000, rollingSeconds: 10);
        rec.Start();
        capture.Push(new short[8000]); // 1 second of capture
        // 1 second stored at 2000 Hz -> ~2000 samples buffered.
        short[] oneSecond = rec.GetRewind(1);
        Assert.InRange(oneSecond.Length, 1900, 2001);
        Assert.Equal(2000, rec.SampleRate);
        rec.Stop();
    }

    [Fact]
    public void SaveQsoAudio_WritesAClip_WhenMonitoring()
    {
        var capture = new FakeCapture();
        var rec = new BandRecorder(capture, captureRateHz: 8000, storeRateHz: 8000, rollingSeconds: 5);
        Assert.Null(rec.SaveQsoAudio("KE4CON")); // not monitoring yet
        rec.Start();
        capture.Push(new short[8000]);
        string? path = rec.SaveQsoAudio("KE4CON");
        rec.Stop();
        try
        {
            Assert.NotNull(path);
            Assert.True(File.Exists(path));
            Assert.Contains("KE4CON", path!);
        }
        finally { if (path is not null && File.Exists(path)) File.Delete(path); }
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


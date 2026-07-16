using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace IcomRigControl.Services;

/// <summary>
/// Plays generated audio samples (e.g. AfskModulator's AFSK tones) through
/// a real Windows audio output device via NAudio. Used to physically key
/// audio into the radio's mic/data input for HF APRS transmission. See
/// CLAUDE.md Phase 10.
/// </summary>
public class NAudioPlayer : IDisposable
{
    private WaveOutEvent? _waveOut;

    public bool IsPlaying { get; private set; }

    /// Lists available audio output devices by friendly name, via WASAPI
    /// (NAudio.CoreAudioApi), which is part of core NAudio and does not
    /// require the separate NAudio.WinForms package (which the older
    /// WaveOut.DeviceCount/GetCapabilities API lives in).
    public List<string> GetAvailableDevices()
    {
        var devices = new List<string>();
        using var enumerator = new MMDeviceEnumerator();

        foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        {
            devices.Add(device.FriendlyName);
        }

        return devices;
    }

    /// Plays the given float samples (-1.0 to 1.0 range) through the
    /// default output device. Awaits completion of playback.
    public async Task PlayAsync(float[] samples, int sampleRateHz)
    {
        var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRateHz, channels: 1);
        var bufferProvider = new SampleToWaveProvider(samples, waveFormat);

        _waveOut = new WaveOutEvent();
        _waveOut.Init(bufferProvider);

        var playbackCompletion = new TaskCompletionSource();
        _waveOut.PlaybackStopped += (_, _) => playbackCompletion.TrySetResult();

        IsPlaying = true;
        _waveOut.Play();

        await playbackCompletion.Task;
        IsPlaying = false;
    }

    public void Stop()
    {
        _waveOut?.Stop();
        IsPlaying = false;
    }

    public void Dispose()
    {
        _waveOut?.Dispose();
    }

    /// Minimal IWaveProvider wrapping a plain float sample array, since
    /// NAudio's WaveOutEvent needs an IWaveProvider, not a raw array.
    private class SampleToWaveProvider : IWaveProvider
    {
        private readonly float[] _samples;
        private int _position;

        public WaveFormat WaveFormat { get; }

        public SampleToWaveProvider(float[] samples, WaveFormat waveFormat)
        {
            _samples = samples;
            WaveFormat = waveFormat;
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            int samplesRequested = count / 4; // 4 bytes per float sample
            int samplesAvailable = _samples.Length - _position;
            int samplesToCopy = Math.Min(samplesRequested, samplesAvailable);

            if (samplesToCopy <= 0) return 0;

            Buffer.BlockCopy(_samples, _position * 4, buffer, offset, samplesToCopy * 4);
            _position += samplesToCopy;

            return samplesToCopy * 4;
        }
    }
}
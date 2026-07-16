using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace IcomRigControl.Services;

/// <summary>
/// Plays generated audio samples (e.g. AfskModulator's AFSK tones) through
/// a real Windows audio output device via NAudio/WASAPI. Used to physically
/// key audio into the radio's mic/data input (or a USB audio interface the
/// radio exposes) for HF APRS transmission. See CLAUDE.md Phase 10.
/// </summary>
public class NAudioPlayer : IAudioPlayer, IDisposable
{
    private WasapiOut? _waveOut;

    public bool IsPlaying { get; private set; }

    /// Lists available audio output devices by friendly name, via WASAPI
    /// (NAudio.CoreAudioApi). The returned names are what should be shown
    /// to the user for selection, and passed back into PlayAsync's
    /// deviceName parameter to route audio to that specific device.
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

    /// Plays the given float samples (-1.0 to 1.0 range) through the named
    /// output device (matched by friendly name from GetAvailableDevices()),
    /// or the system default device if deviceName is null/empty or no match
    /// is found. Awaits completion of playback.
    public async Task PlayAsync(float[] samples, int sampleRateHz, string? deviceName = null)
    {
        var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRateHz, channels: 1);
        var bufferProvider = new SampleToWaveProvider(samples, waveFormat);

        using var enumerator = new MMDeviceEnumerator();
        MMDevice selectedDevice = FindDeviceByName(enumerator, deviceName)
            ?? enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

        _waveOut = new WasapiOut(selectedDevice, AudioClientShareMode.Shared, useEventSync: true, latency: 200);
        _waveOut.Init(bufferProvider);

        var playbackCompletion = new TaskCompletionSource();
        _waveOut.PlaybackStopped += (_, _) => playbackCompletion.TrySetResult();

        IsPlaying = true;
        _waveOut.Play();

        await playbackCompletion.Task;
        IsPlaying = false;
    }

    private static MMDevice? FindDeviceByName(MMDeviceEnumerator enumerator, string? deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName)) return null;

        foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        {
            if (device.FriendlyName.Equals(deviceName, StringComparison.OrdinalIgnoreCase))
            {
                return device;
            }
        }

        return null;
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
    /// NAudio's WasapiOut needs an IWaveProvider, not a raw array.
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
namespace IcomRigControl.Services;

/// <summary>
/// Streams 16-bit mono PCM to a WAV (RIFF) file — pure managed code, so it works on
/// Windows, macOS, Linux, and the Pi with no audio library. It writes a placeholder
/// header, appends samples as they arrive (so a long recording doesn't sit in
/// memory), and patches the RIFF/data sizes on Close. Used by the Band DVR to record
/// the radio's receive audio. See CLAUDE.md Band DVR.
/// </summary>
public sealed class WavWriter : IDisposable
{
    private readonly FileStream _fs;
    private readonly int _sampleRate;
    private int _dataBytes;
    private bool _closed;

    public string Path { get; }

    public WavWriter(string path, int sampleRateHz)
    {
        Path = path;
        _sampleRate = sampleRateHz;
        _fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        WriteHeader(0); // placeholder; patched on Close
    }

    public void Write(short[] samples)
    {
        if (_closed || samples.Length == 0) return;
        var bytes = new byte[samples.Length * 2];
        Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
        _fs.Write(bytes, 0, bytes.Length);
        _dataBytes += bytes.Length;
    }

    public void Close()
    {
        if (_closed) return;
        _closed = true;
        _fs.Seek(0, SeekOrigin.Begin);
        WriteHeader(_dataBytes); // real sizes
        _fs.Flush();
        _fs.Dispose();
    }

    public void Dispose() => Close();

    private void WriteHeader(int dataBytes)
    {
        const short channels = 1, bitsPerSample = 16;
        int byteRate = _sampleRate * channels * bitsPerSample / 8;
        short blockAlign = channels * bitsPerSample / 8;

        var w = new BinaryWriter(_fs, System.Text.Encoding.ASCII, leaveOpen: true);
        w.Write("RIFF"u8.ToArray());
        w.Write(36 + dataBytes);          // file size - 8
        w.Write("WAVE"u8.ToArray());
        w.Write("fmt "u8.ToArray());
        w.Write(16);                      // fmt chunk size
        w.Write((short)1);                // PCM
        w.Write(channels);
        w.Write(_sampleRate);
        w.Write(byteRate);
        w.Write(blockAlign);
        w.Write(bitsPerSample);
        w.Write("data"u8.ToArray());
        w.Write(dataBytes);
        w.Flush();
    }

    /// One-shot convenience: write a complete buffer to a WAV file.
    public static void WriteFile(string path, short[] samples, int sampleRateHz)
    {
        using var writer = new WavWriter(path, sampleRateHz);
        writer.Write(samples);
    }
}

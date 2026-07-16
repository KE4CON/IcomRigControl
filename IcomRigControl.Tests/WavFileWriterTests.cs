using System.Text;
using IcomRigControl.CivEngine;
using Xunit;

namespace IcomRigControl.Tests;

public class WavFileWriterTests
{
    [Fact]
    public void WriteToBytes_StartsWithRiffHeader()
    {
        float[] samples = { 0.0f, 0.5f, -0.5f };
        byte[] wav = WavFileWriter.WriteToBytes(samples, sampleRateHz: 44100);

        Assert.Equal("RIFF", Encoding.ASCII.GetString(wav, 0, 4));
    }

    [Fact]
    public void WriteToBytes_ContainsWaveFormatIdentifier()
    {
        float[] samples = { 0.0f };
        byte[] wav = WavFileWriter.WriteToBytes(samples, sampleRateHz: 44100);

        Assert.Equal("WAVE", Encoding.ASCII.GetString(wav, 8, 4));
    }

    [Fact]
    public void WriteToBytes_ContainsFmtChunk()
    {
        float[] samples = { 0.0f };
        byte[] wav = WavFileWriter.WriteToBytes(samples, sampleRateHz: 44100);

        Assert.Equal("fmt ", Encoding.ASCII.GetString(wav, 12, 4));
    }

    [Fact]
    public void WriteToBytes_FmtChunkDeclaresPcmMonoAtGivenSampleRate()
    {
        float[] samples = { 0.0f };
        byte[] wav = WavFileWriter.WriteToBytes(samples, sampleRateHz: 44100);

        // AudioFormat (offset 20, 2 bytes LE) = 1 for PCM
        short audioFormat = BitConverter.ToInt16(wav, 20);
        Assert.Equal(1, audioFormat);

        // NumChannels (offset 22, 2 bytes LE) = 1 for mono
        short numChannels = BitConverter.ToInt16(wav, 22);
        Assert.Equal(1, numChannels);

        // SampleRate (offset 24, 4 bytes LE)
        int sampleRate = BitConverter.ToInt32(wav, 24);
        Assert.Equal(44100, sampleRate);

        // BitsPerSample (offset 34, 2 bytes LE) = 16
        short bitsPerSample = BitConverter.ToInt16(wav, 34);
        Assert.Equal(16, bitsPerSample);
    }

    [Fact]
    public void WriteToBytes_ContainsDataChunkWithCorrectSize()
    {
        float[] samples = { 0.0f, 0.5f, -0.5f, 1.0f };
        byte[] wav = WavFileWriter.WriteToBytes(samples, sampleRateHz: 44100);

        Assert.Equal("data", Encoding.ASCII.GetString(wav, 36, 4));

        // data chunk size (offset 40, 4 bytes LE) = sample count * 2 bytes (16-bit)
        int dataSize = BitConverter.ToInt32(wav, 40);
        Assert.Equal(samples.Length * 2, dataSize);
    }

    [Fact]
    public void WriteToBytes_TotalFileSizeMatchesHeaderPlusData()
    {
        float[] samples = { 0.0f, 0.5f, -0.5f, 1.0f, -1.0f };
        byte[] wav = WavFileWriter.WriteToBytes(samples, sampleRateHz: 44100);

        // 44-byte header + (sample count * 2 bytes for 16-bit PCM)
        int expectedTotal = 44 + samples.Length * 2;
        Assert.Equal(expectedTotal, wav.Length);
    }

    [Fact]
    public void WriteToBytes_SampleValuesAreCorrectlyScaledTo16Bit()
    {
        // A sample of exactly 1.0 should map to Int16.MaxValue (32767),
        // and -1.0 should map to Int16.MinValue (-32768).
        float[] samples = { 1.0f, -1.0f, 0.0f };
        byte[] wav = WavFileWriter.WriteToBytes(samples, sampleRateHz: 44100);

        short first = BitConverter.ToInt16(wav, 44);
        short second = BitConverter.ToInt16(wav, 46);
        short third = BitConverter.ToInt16(wav, 48);

        Assert.Equal(short.MaxValue, first);
        Assert.Equal(short.MinValue, second);
        Assert.Equal(0, third);
    }

    [Fact]
    public void WriteToFile_CreatesReadableFileOnDisk()
    {
        float[] samples = { 0.0f, 0.5f, -0.5f };
        var tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid() + ".wav");

        try
        {
            WavFileWriter.WriteToFile(tempPath, samples, sampleRateHz: 44100);

            Assert.True(System.IO.File.Exists(tempPath));
            var bytesOnDisk = System.IO.File.ReadAllBytes(tempPath);
            Assert.Equal("RIFF", Encoding.ASCII.GetString(bytesOnDisk, 0, 4));
        }
        finally
        {
            if (System.IO.File.Exists(tempPath)) System.IO.File.Delete(tempPath);
        }
    }
}
namespace IcomRigControl.Services;

/// <summary>
/// Reframes an arbitrary stream of captured PCM samples into fixed-size frames
/// for the codec. Audio devices hand back buffers of whatever size they like;
/// Opus needs exactly FrameSize samples per encode. Add() buffers the input and
/// returns every complete frame it can form, keeping the remainder for next time.
/// Not thread-safe — call from a single producer (the capture callback).
/// </summary>
public class FrameAccumulator
{
    private readonly int _frameSize;
    private readonly List<short> _buffer = new();

    public FrameAccumulator(int frameSize) => _frameSize = frameSize;

    public List<short[]> Add(short[] samples)
    {
        _buffer.AddRange(samples);

        var frames = new List<short[]>();
        while (_buffer.Count >= _frameSize)
        {
            frames.Add(_buffer.GetRange(0, _frameSize).ToArray());
            _buffer.RemoveRange(0, _frameSize);
        }
        return frames;
    }
}

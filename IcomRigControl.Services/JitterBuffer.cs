using System.Linq;

namespace IcomRigControl.Services;

/// <summary>
/// A small play-out buffer for the remote-audio stream (Phase 12). UDP packets
/// can arrive out of order, late, or not at all; this reorders them by sequence
/// number and hands them back in order at play-out time. It primes to a target
/// depth before releasing anything (absorbing jitter), conceals a missing frame
/// by returning null (the caller runs Opus PLC / plays silence), and resyncs if
/// it falls badly behind (large discontinuity or sequence wrap).
///
/// Not thread-safe: Add and GetNext are expected to be called from one owner
/// (typically the audio play-out loop pulling, feeding from the receive loop via
/// its own synchronization).
/// </summary>
public class JitterBuffer
{
    private readonly SortedDictionary<ushort, byte[]> _buffer = new();
    private readonly int _targetDepth;
    private ushort _nextSequence;
    private bool _primed;

    public int Count => _buffer.Count;

    public JitterBuffer(int targetDepth = 3)
    {
        _targetDepth = targetDepth < 1 ? 1 : targetDepth;
    }

    /// Add a received packet. Later duplicates of the same sequence overwrite.
    public void Add(ushort sequence, byte[] payload) => _buffer[sequence] = payload;

    /// Pull the next frame for play-out, in sequence order:
    ///  - a payload when the next expected frame is available,
    ///  - null while still priming or on underrun (nothing buffered), or
    ///  - null when the next expected frame is missing but later frames are
    ///    waiting (a gap to conceal) — the sequence still advances past it.
    /// </summary>
    public byte[]? GetNext()
    {
        if (_buffer.Count == 0) return null;

        if (!_primed)
        {
            if (_buffer.Count < _targetDepth) return null; // keep filling
            _primed = true;
            _nextSequence = _buffer.Keys.First(); // lowest buffered sequence
        }

        // If we've fallen far behind (big discontinuity or wrap), jump forward.
        if (_buffer.Count > _targetDepth * 4)
            _nextSequence = _buffer.Keys.First();

        if (_buffer.Remove(_nextSequence, out var payload))
        {
            _nextSequence++;
            return payload;
        }

        // Expected frame is missing but others are waiting: conceal this one.
        _nextSequence++;
        return null;
    }
}

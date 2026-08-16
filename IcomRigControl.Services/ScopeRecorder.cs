using IcomRigControl.RigModel;

namespace IcomRigControl.Services;

/// <summary>
/// The waterfall half of the Band DVR: keeps a rolling history of the most recent
/// spectrum-scope frames so you can "rewind" and see what the band looked like a
/// moment ago. It only listens to the Transceiver's existing WaveformUpdated event
/// (the scope is already running), so it costs nothing extra and never opens the
/// audio device. See CLAUDE.md Band DVR waterfall.
/// </summary>
public sealed class ScopeRecorder : IDisposable
{
    private readonly Transceiver _transceiver;
    private readonly int _capacity;
    private readonly object _lock = new();
    private readonly Queue<int[]> _frames = new();

    /// <param name="maxFrames">How many scope frames to keep. At ~3 frames/second
    /// (300 ms sweeps), 300 frames is about 90 seconds of history.</param>
    public ScopeRecorder(Transceiver transceiver, int maxFrames = 300)
    {
        _transceiver = transceiver;
        _capacity = Math.Max(1, maxFrames);
        _transceiver.WaveformUpdated += OnWaveform;
    }

    public int FrameCount { get { lock (_lock) return _frames.Count; } }

    private void OnWaveform(object? sender, int[] frame) => Add(frame);

    /// Adds a scope frame to the history ring (public so the buffering logic is
    /// directly testable; the Transceiver's WaveformUpdated event routes here).
    public void Add(int[] frame)
    {
        if (frame.Length == 0) return;
        lock (_lock)
        {
            _frames.Enqueue((int[])frame.Clone());
            while (_frames.Count > _capacity) _frames.Dequeue();
        }
    }

    /// The buffered frames, oldest first — replay them into a waterfall to see the
    /// recent history.
    public IReadOnlyList<int[]> GetFrames()
    {
        lock (_lock) return _frames.ToList();
    }

    public void Clear()
    {
        lock (_lock) _frames.Clear();
    }

    public void Dispose()
    {
        _transceiver.WaveformUpdated -= OnWaveform;
    }
}

using IcomRigControl.RigModel;

namespace IcomRigControl.Services;

/// <summary>
/// A rig-saver: watches the SWR meter while transmitting and, if it exceeds a
/// threshold, immediately unkeys and engages the Receive-Only / TX-inhibit guard to
/// protect the radio's finals from a bad antenna or tuner. The operator clears the
/// inhibit (the existing dashboard banner) once the fault is fixed. See CLAUDE.md
/// SWR guard.
/// </summary>
public sealed class SwrProtection : IDisposable
{
    private readonly Transceiver _rig;

    public bool Enabled { get; set; }
    public double Threshold { get; set; } = 3.0; // 3:1 default

    /// Raised with the offending SWR when protection trips.
    public event Action<double>? Tripped;

    public SwrProtection(Transceiver rig)
    {
        _rig = rig;
        _rig.MeterUpdated += OnMeter;
    }

    /// Pure decision: trip only when enabled, actually transmitting, and SWR is at or
    /// above a sane positive threshold.
    public static bool ShouldTrip(bool enabled, bool transmitting, double swr, double threshold) =>
        enabled && transmitting && threshold > 0 && swr >= threshold;

    private void OnMeter(object? sender, MeterSnapshot m)
    {
        if (!ShouldTrip(Enabled, _rig.PttActive, m.SwrRatio, Threshold)) return;
        _ = _rig.SetPttAsync(false);      // unkey immediately
        _rig.TransmitInhibited = true;    // block further TX until the op clears it
        Tripped?.Invoke(m.SwrRatio);
    }

    public void Dispose() => _rig.MeterUpdated -= OnMeter;
}

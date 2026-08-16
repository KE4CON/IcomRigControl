namespace IcomRigControl.Services;

/// <summary>
/// A single DX cluster spot: a report that a station (DxCallsign) was heard on
/// a frequency, posted by another operator (Spotter). Frequencies from clusters
/// are in kHz; FrequencyHz is the canonical value the rest of the app uses.
/// </summary>
public record DxSpot(
    string DxCallsign,
    long FrequencyHz,
    string Spotter,
    string Comment,
    string TimeUtc)
{
    /// Frequency in kHz (as clusters report it), for display.
    public double FrequencyKHz => FrequencyHz / 1000.0;

    /// Set by the DX-cluster view when the spot is a station not yet worked on this
    /// band (see SpotNeedAnalyzer) — drives the "NEW" highlight.
    public bool IsNew { get; set; }
}

namespace IcomRigControl.Services;

/// <summary>
/// Describes the rules for a single contest: exchange field labels, valid
/// bands/modes, and scoring logic. Instances live in ContestCatalog.
/// </summary>
public class ContestDefinition
{
    public required string Name { get; init; }
    public required List<string> ExchangeFieldLabels { get; init; }

    /// Empty list means no restriction — all bands allowed.
    public List<string> RestrictedBands { get; init; } = new();

    /// Points awarded per QSO based on mode. Digital modes (CW, FT8, etc.)
    /// commonly score higher than phone in ARRL-style contests.
    public required Func<string, int> PointsForMode { get; init; }

    /// Determines whether a proposed QSO (callsign/band/mode) is a duplicate
    /// of an already-logged contact under this contest's dupe rules.
    public bool IsDuplicate(IEnumerable<QsoRecord> existingQsos, string callsign, string band, string mode)
    {
        string normalizedCall = callsign.ToUpperInvariant();
        return existingQsos.Any(q =>
            q.Callsign.Equals(normalizedCall, StringComparison.OrdinalIgnoreCase) &&
            q.Band.Equals(band, StringComparison.OrdinalIgnoreCase));
    }
}
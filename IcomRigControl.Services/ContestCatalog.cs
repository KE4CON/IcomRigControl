namespace IcomRigControl.Services;

/// <summary>
/// Built-in catalog of known contest definitions. Starts with ARRL Field Day;
/// additional contests can be added incrementally as ContestDefinition instances.
/// </summary>
public static class ContestCatalog
{
    /// ARRL Field Day: exchange is Class + Section (e.g. "3A GA").
    /// Scoring: CW and digital modes (FT8, RTTY) = 2 points/QSO, phone = 1 point/QSO.
    /// All bands and modes allowed; dupe = same station worked again on same band
    /// (regardless of mode, per official Field Day rules).
   public static readonly ContestDefinition FieldDay = new()
    {
        Name = "ARRL Field Day",
        ExchangeFieldLabels = new List<string> { "Class", "Section" },
        RestrictedBands = new List<string>(), // all bands allowed
        PointsForMode = mode => mode.ToUpperInvariant() switch
        {
            "CW" => 2,
            "FT8" => 2,
            "FT4" => 2,
            "RTTY" => 2,
            _ => 1 // phone modes: USB, LSB, AM, FM
        }
    };

    /// ARRL RTTY Roundup: RTTY-only contest, first full weekend of January
    /// (never on Jan 1). Exchange is RST + State/Province for W/VE stations,
    /// or RST + serial number (starting at 1) for DX stations -- both fit
    /// in the single "Exchange" field since the meaning is determined by
    /// who's on the other end, not by a separate schema. Scoring: 1 point
    /// per QSO (flat, no mode-based bonus, since only one mode is allowed).
    /// Verified against the official ARRL RTTY Roundup rules (contests.arrl.org).
    public static readonly ContestDefinition RttyRoundup = new()
    {
        Name = "ARRL RTTY Roundup",
        ExchangeFieldLabels = new List<string> { "RST", "Exchange (State/Prov or Serial#)" },
        RestrictedBands = new List<string> { "80M", "40M", "20M", "15M", "10M" },
        PointsForMode = mode => 1 // RTTY-only contest -- every QSO is worth exactly 1 point
    };
}
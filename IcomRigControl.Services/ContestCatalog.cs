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
}
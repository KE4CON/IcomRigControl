namespace IcomRigControl.Services;

/// <summary>
/// Common result shape returned by any callsign lookup source, so the UI
/// and QsoLogger can consume QRZ/HamQTH/Callook interchangeably.
/// </summary>
public record CallsignInfo(
    string Callsign,
    string? Name,
    string? GridSquare,
    string? Address,
    string? LicenseClass
);

/// <summary>
/// A pluggable source for looking up amateur radio callsign information
/// (name, grid square, address). Implementations must never throw — a
/// failed or unavailable lookup returns null, never an exception, so a
/// lookup failure never blocks manual QSO logging (CLAUDE.md Phase 8c).
/// </summary>
public interface ICallsignLookupSource
{
    /// Human-readable name of this source, for display in Settings (e.g. "QRZ.com", "HamQTH", "Callook.info").
    string SourceName { get; }

    /// True if this source needs the user's own account credentials configured before use.
    bool RequiresCredentials { get; }

    /// Looks up a callsign. Returns null if not found, on any network error,
    /// or if the response could not be parsed — never throws.
    Task<CallsignInfo?> LookupAsync(string callsign);
}
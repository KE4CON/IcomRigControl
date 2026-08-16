namespace IcomRigControl.Services;

/// <summary>
/// Cross-references DX cluster spots against your own QSO log to flag the ones worth
/// chasing: a station you have not worked on that band ("new one on 20m!"), or never
/// worked at all. Built from a snapshot of the log; rebuild it to pick up new
/// contacts made during the session. See CLAUDE.md spot alerts.
/// </summary>
public sealed class SpotNeedAnalyzer
{
    private readonly HashSet<string> _calls = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _callBands = new(StringComparer.OrdinalIgnoreCase);

    public SpotNeedAnalyzer(IEnumerable<QsoRecord> qsos)
    {
        foreach (var q in qsos)
        {
            if (string.IsNullOrWhiteSpace(q.Callsign)) continue;
            _calls.Add(q.Callsign);
            _callBands.Add(Key(q.Callsign, q.Band));
        }
    }

    /// True if this callsign has never been logged (a brand-new station).
    public bool IsNewCall(string callsign) => !_calls.Contains(callsign);

    /// True if this callsign has not been worked on the band containing this
    /// frequency — the usual "need it on this band" case.
    public bool IsNewOnBand(string callsign, long frequencyHz) =>
        !_callBands.Contains(Key(callsign, Bands.FromHz(frequencyHz)));

    private static string Key(string call, string band) => $"{call.Trim().ToUpperInvariant()}|{band}";
}

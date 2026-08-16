namespace IcomRigControl.Services;

/// A worked contact reduced to what award tracking needs.
public record WorkedContact(string Call, string Band, string? Grid = null, string? State = null);

/// <summary>
/// Computes award progress from your worked contacts: which DXCC entities and grid
/// squares you've worked, and whether a given callsign is a "new one" (an entity you
/// haven't worked). Contacts come from IcomRigControl's own log AND your HRD logbook,
/// so it reflects your full operating history. See CLAUDE.md award tracking.
/// </summary>
public sealed class AwardTracker
{
    /// The 50 US states (2-letter) for Worked All States. DC is not a WAS "state".
    public static readonly IReadOnlySet<string> UsStates = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "AL","AK","AZ","AR","CA","CO","CT","DE","FL","GA","HI","ID","IL","IN","IA","KS","KY","LA","ME","MD",
        "MA","MI","MN","MS","MO","MT","NE","NV","NH","NJ","NM","NY","NC","ND","OH","OK","OR","PA","RI","SC",
        "SD","TN","TX","UT","VT","VA","WA","WV","WI","WY",
    };

    public IReadOnlySet<string> Entities { get; }
    public IReadOnlySet<string> Grids { get; }
    public IReadOnlySet<string> States { get; }

    public AwardTracker(IEnumerable<WorkedContact> contacts)
    {
        var entities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var grids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var states = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in contacts)
        {
            string entity = DxccResolver.Resolve(c.Call);
            if (entity != "Unknown") entities.Add(entity);

            if (!string.IsNullOrWhiteSpace(c.Grid))
            {
                string g = c.Grid.Trim().ToUpperInvariant();
                grids.Add(g.Length >= 4 ? g[..4] : g); // grid "field+square" (e.g. FN31)
            }

            if (!string.IsNullOrWhiteSpace(c.State))
            {
                string st = c.State.Trim().ToUpperInvariant();
                if (UsStates.Contains(st)) states.Add(st); // only count real states
            }
        }
        Entities = entities;
        Grids = grids;
        States = states;
    }

    public int EntityCount => Entities.Count;
    public int GridCount => Grids.Count;
    public int StateCount => States.Count;

    /// True if the callsign's DXCC entity hasn't been worked (a "new one").
    public bool IsNewEntity(string callsign)
    {
        string entity = DxccResolver.Resolve(callsign);
        return entity != "Unknown" && !Entities.Contains(entity);
    }

    public string EntityOf(string callsign) => DxccResolver.Resolve(callsign);
}

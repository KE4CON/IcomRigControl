namespace IcomRigControl.Services;

/// <summary>
/// All user-configurable settings for the external integrations built in
/// Phase 8 (callsign lookup, LoTW, HRD, N1MM/WSJT-X). Serialized to a local
/// JSON file by SettingsService — never committed to source control (see
/// CLAUDE.md's credential-storage rule).
/// </summary>
public class AppSettings
{
    // ── Phase 8c: Callsign lookup ────────────────────────────────────────
    public string CallsignLookupSource { get; set; } = "Callook"; // "Callook", "QRZ", or "HamQTH"
    public string QrzUsername { get; set; } = "";
    public string QrzPassword { get; set; } = "";
    public string HamQthUsername { get; set; } = "";
    public string HamQthPassword { get; set; } = "";

    // ── Phase 8d: LoTW ────────────────────────────────────────────────────
    public string TqslExecutablePath { get; set; } = "";

    // ── Phase 8e: HRD Logbook ────────────────────────────────────────────
    public bool HrdBridgeEnabled { get; set; } = false;
    public string HrdDatabasePath { get; set; } = "";

    // ── Phase 8f: N1MM / WSJT-X UDP integration ─────────────────────────
    public bool N1mmSendEnabled { get; set; } = false;
    public bool N1mmReceiveEnabled { get; set; } = false;
    public List<(string Ip, int Port)> N1mmDestinations { get; set; } = new();
    public int ContactListenPort { get; set; } = 12070;
}
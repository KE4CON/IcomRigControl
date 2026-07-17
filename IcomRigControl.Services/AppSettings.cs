namespace IcomRigControl.Services;

/// <summary>
/// All user-configurable settings for the external integrations built in
/// Phase 8, the remote connection mode built in Phase 9, and the APRS
/// beacon settings built in Phase 10. Serialized to a local JSON file by
/// SettingsService — never committed to source control (see CLAUDE.md's
/// credential-storage rule).
/// </summary>
public class AppSettings
{
    // ── Phase 9: Radio connection mode ──────────────────────────────────
    /// "Demo" (default, no hardware needed), "Serial" (local USB-connected
    /// radio), or "Remote" (connect over TCP to a CivTcpServer, e.g. a Pi
    /// running --headless-server, possibly over 44Net/AMPRNet).
    public string ConnectionMode { get; set; } = "Demo";
    public string SerialPortName { get; set; } = "";
    public string RemoteHost { get; set; } = "";
    public int RemotePort { get; set; } = 7300;
    public string RemoteAuthToken { get; set; } = "";

    // ── Phase 10: APRS beacon settings ──────────────────────────────────
    public string AprsCallsign { get; set; } = "";
    public int AprsSsid { get; set; } = 9; // 9 is the conventional SSID for mobile/HF APRS
    public char AprsSymbolTable { get; set; } = '/';
    public char AprsSymbolCode { get; set; } = '>'; // car/mobile symbol
    public string AprsComment { get; set; } = "";
    public double AprsLatitude { get; set; } = 0;
    public double AprsLongitude { get; set; } = 0;
    public string AudioOutputDeviceName { get; set; } = ""; // empty = system default
    public int AprsBeaconIntervalMinutes { get; set; } = 10; // 0 = manual only, no auto-beacon

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
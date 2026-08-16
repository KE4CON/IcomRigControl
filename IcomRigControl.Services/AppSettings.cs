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
    // ── Receive-only / TX inhibit ────────────────────────────────────────
    /// When true the app will not key the radio for anything (e.g. while another
    /// program owns transmit). Persisted so the choice survives a restart — a
    /// bold on-screen banner makes the state impossible to miss.
    public bool TransmitInhibited { get; set; } = false;

    // ── Radio model ──────────────────────────────────────────────────────
    /// Which supported radio this station uses: "IC7300" (default), "IC7300MK2",
    /// or "IC705". Selects the CI-V address (0x94 / 0xB6 / 0xA4). Changing it
    /// takes effect on the next app start (the Transceiver is built at startup).
    public string RadioModel { get; set; } = "IC7300";

    // ── Phase 9: Radio connection mode ──────────────────────────────────
    /// "Demo" (default, no hardware needed), "Serial" (local USB-connected
    /// radio), or "Remote" (connect over TCP to a CivTcpServer, e.g. a Pi
    /// running --headless-server, possibly over 44Net/AMPRNet).
    public string ConnectionMode { get; set; } = "Demo";
    public string SerialPortName { get; set; } = "";
    public string RemoteHost { get; set; } = "";
    public int RemotePort { get; set; } = 7300;
    public string RemoteAuthToken { get; set; } = "";

    // ── Web / phone-tablet remote (browser client) ───────────────────────
    // Serves a mobile web UI (tuning, meters, PTT, later scope + audio) that
    // any phone/tablet browser on the network can open. Token-gated; use over a
    // trusted LAN or VPN. Port 8080 is the conventional alternate-HTTP port.
    public int WebRemotePort { get; set; } = 8080;
    public string WebRemoteToken { get; set; } = ""; // empty = no token required (LAN only)
    // Serve the web remote over HTTPS (self-signed). Required for browser microphone
    // access, so "Hold to Talk" works from a phone over the LAN (not just localhost).
    public bool WebRemoteUseHttps { get; set; } = false;

    // Phase 12 remote audio: the server's UDP audio port (the desktop client
    // reuses RemoteHost above as the server host) and the client's capture (mic)
    // device.
    public int RemoteAudioPort { get; set; } = 7301;
    public string RemoteAudioCaptureDevice { get; set; } = "";

    // ── CW decode / zero beat ────────────────────────────────────────────
    // The receiver's CW pitch (Hz) — the tone the reader decodes and the target
    // for zero-beat. Match this to the radio's own CW Pitch menu setting.
    public double CwPitchHz { get; set; } = 600;
    // Flip the zero-beat tuning direction for CW-Reverse. Default suits CW-Normal.
    public bool CwZeroBeatReverse { get; set; } = false;

    // RTTY decode: swap mark/space (wrong-sideband / reversed signal).
    public bool RttyReverse { get; set; } = false;

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
    // Auto-reply ":ackNN" when someone sends us a numbered APRS message.
    public bool AprsAutoAck { get; set; } = true;

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

    // ── DX Cluster ───────────────────────────────────────────────────────
    /// DX cluster telnet host (e.g. "dxc.nc7j.com"), port, and the callsign used
    /// to log in. Saved when you connect from the DX Cluster window.
    public string DxClusterHost { get; set; } = "";
    public int DxClusterPort { get; set; } = 7373;
    public string DxClusterLoginCall { get; set; } = "";

    // ── CW keyer message memories (sent via CI-V command 17) ────────────
    /// Up to 8 editable CW macros. "MYCALL" is a placeholder to replace with a
    /// real callsign. Each is capped at 30 characters when transmitted.
    public List<string> CwMessages { get; set; } = new()
    {
        "CQ CQ CQ DE MYCALL MYCALL K",
        "MYCALL",
        "R TU 73 GL",
        "QRZ? DE MYCALL",
        "", "", "", ""
    };
}
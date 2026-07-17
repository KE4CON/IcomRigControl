The server prints status to the console and runs until you press Ctrl+C.

### 9.3 Connecting a Desktop Client Remotely
On the connecting machine, open Settings, set Connection Mode to Remote, enter the server's host/IP, the same TCP port, and the same token used when starting the server. Save and restart the app.

### 9.4 Security Note
A connection token is always required - the server rejects any connection without the correct token, using a constant-time comparison to avoid leaking timing information. Never leave the token blank; the server will not run unauthenticated.

---

## 10. APRS Beacon

### 10.1 Setup
In Settings, under "APRS Beacon": enter your callsign, SSID (9 is conventional for mobile/HF APRS), symbol table/code, an optional comment, your latitude/longitude, and select an audio output device - this should be whatever audio interface is actually connected to your radio (its USB audio device, or an external soundcard wired to the mic/data port). Also set an Auto-Beacon Interval in minutes if you want automatic periodic beaconing (0 = manual only).

### 10.2 Sending a Beacon Manually
Click Send Beacon on the main dashboard. IcomRigControl builds an APRS position report, keys PTT, plays the generated AFSK tones through your selected audio device, then releases PTT - guaranteed, even if something goes wrong mid-transmission, so the radio never gets stuck transmitting.

### 10.3 Automatic Periodic Beaconing
Click Auto Beacon to start automatic beaconing at the interval configured in Settings. Click it again to stop. The status text shows the current state and interval.

### 10.4 What's in the Beacon
Your configured comment, plus your radio's current live frequency and mode, are both included in every beacon's comment field automatically.

### 10.5 Technical Notes
Uses real-world HF APRS tones (1600/1800 Hz at 300 baud, matching what DireWolf and other HF APRS software actually use in practice) rather than the historical literal Bell 103 telephone-modem frequencies, which would not be decodable by real HF APRS listening stations.

### 10.6 Platform Support
Currently Windows only (via NAudio/WASAPI). macOS support (AVFoundation) is planned but not yet built.

---

## 11. Settings Window Reference

Open via the Settings button on the main dashboard. Settings auto-close the window on successful save.

- **Radio Connection**: Demo/Serial/Remote mode, serial port, remote host/port/token.
- **APRS Beacon**: callsign, SSID, symbol, comment, position, audio device, auto-beacon interval.
- **Callsign Lookup**: source selection and credentials.
- **Logbook of the World**: TQSL executable path.
- **Ham Radio Deluxe**: direct-write bridge toggle and database path.
- **N1MM / WSJT-X Integration**: send/receive toggles, destination, listen port.

---

## 12. Troubleshooting

### 12.1 Finding Your Radio's Serial Port Name
Windows: Device Manager, Ports. macOS: ls /dev/tty.usb*

### 12.2 App Won't Connect to the Radio
Check power, cable, port conflicts, baud rate, and that Connection Mode in Settings is actually set to Serial (not Demo).

### 12.3 Documents Files Not Where Expected
Check the OneDrive-redirected path (e.g. C:\Users\<you>\OneDrive\...\Documents\IcomRigControl\ rather than the plain Documents folder).

### 12.4 Memory Editor Table Looks Different
Intentional workaround for a grid rendering bug in the underlying UI framework.

### 12.5 Waterfall Blank or Not Filling Panel
Known issue, fixed at the source.

### 12.6 EMMCOM or External Integration Shows Red/Error
Network endpoint unreachable, keeps retrying, never crashes.

### 12.7 Settings Don't Seem to Take Effect
As of this version, Settings auto-closes and re-applies on save for most options. Connection Mode specifically requires a full app restart, since it determines how the radio connection is set up when the app starts.

### 12.8 Auto Beacon Won't Start
Check that a callsign is configured and the Auto-Beacon Interval in Settings is greater than 0.

---

## 13. Field / EMCOMM Deployment Notes

- Resilient local QSO logging is deliberate EMCOMM-style redundancy - your log is never dependent on any external program being available.
- The headless server mode (section 9) is designed for exactly this kind of deployment: a Pi at the radio, reachable over 44Net/AMPRNet or a local mesh/VPN, with the operator's desktop app connecting remotely.
- Recommended deployment: Raspberry Pi 5, 8GB RAM.
- Use an A2-rated microSD card for sustained write performance.

---

## 14. Revision History

2026-07-14: Initial manual created, covering Phases 1 through 8f (Direction 2: receiving external QSOs).
2026-07-14: Updated section 8 - Phase 8f Direction 1 (sending rig status) now also complete at the engine level.
2026-07-17: Full rewrite covering Phase 8's complete state (callsign lookup, LoTW, HRD, N1MM/WSJT-X all fully built and reachable through the UI), Phase 9 (remote/network mode, headless server), and Phase 10 (APRS beacon, manual and automatic, Windows-complete with macOS audio support planned).
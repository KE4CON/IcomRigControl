# IcomRigControl User Manual

**For:** Jim, KE4CON
**Covers:** Phases 1-10, complete on both Windows and macOS

---

## Table of Contents

1. Getting Started
2. The Main Dashboard
3. Memory Channel Editor
4. Activity Logging (CSV)
5. Spectrum Waterfall Display
6. EMMCOM Field Comms Server Integration
7. QSO Logging and Contest Mode
8. External Program Integration
9. Remote/Network Mode
10. APRS Beacon
11. Settings Window Reference
12. Troubleshooting
13. Field / EMCOMM Deployment Notes
14. Revision History

---

## 1. Getting Started

### 1.1 What IcomRigControl Does

IcomRigControl is a cross-platform desktop application (Windows, macOS, Linux, Raspberry Pi) for controlling an Icom IC-7300 or IC-7300MK2 over its CI-V interface, locally via USB or remotely over a network. Beyond basic rig control, it provides:

- Live meter monitoring (S-meter, power, SWR, ALC, voltage, current)
- A bulk memory channel editor
- CSV activity logging
- A live spectrum waterfall display with frequency axis labels and click-to-tune
- Integration with an EMMCOM Field Comms Server
- ADIF-based QSO logging, with contest mode (ARRL Field Day and ARRL RTTY Roundup), callsign lookup, LoTW upload/download, HRD Logbook integration, and N1MM/WSJT-X two-way integration
- Remote rig control over a network (including 44Net/AMPRNet), with a headless server mode for running on a Raspberry Pi at the radio
- APRS beacon transmission over HF, including automatic periodic beaconing - fully working on both Windows and macOS

### 1.2 System Requirements

- Windows, macOS, Linux desktop, or Raspberry Pi OS (ARM64)
- .NET 10 runtime
- An Icom IC-7300 or IC-7300MK2 connected via USB (for local control), or network access to a machine running IcomRigControl's headless server (for remote control)

### 1.3 First Launch

1. Launch IcomRigControl. By default it starts in Demo mode (no hardware needed) so you can explore the app safely.
2. Open Settings and set Connection Mode to Serial (for a local USB-connected radio) or Remote (to connect to a headless server elsewhere on your network). See section 9 for Remote mode details.
3. Connection mode changes require restarting the app to take effect.

### 1.4 Radio Address Reference

IC-7300: CI-V address 0x94
IC-7300MK2: CI-V address 0xB6

---

## 2. The Main Dashboard

### 2.1 Status Row
Shows connection status text.

### 2.2 Frequency Entry
Type a frequency in Hz and click Set Freq to tune the radio.

### 2.3 Frequency Display
Large green digital readout. Mode buttons (LSB/USB/AM/CW/FM) change mode instantly.

### 2.4 Spectrum Scope / Waterfall
See section 5.

### 2.5 PTT Indicator
Red dot = transmitting, gray = receiving. Toggle PTT button manually keys/unkeys the radio.

### 2.6 Meters Grid
S-Meter, Power, SWR, ALC, Supply Voltage, Current Draw - updating roughly twice per second.

### 2.7 Activity Logging Control
See section 4.

### 2.8 EMMCOM Control
See section 6.

### 2.9 APRS Beacon Controls
Send Beacon and Auto Beacon buttons. See section 10.

### 2.10 Integrations Status
Summary line showing which external integrations are currently active.

### 2.11 Open Memory Editor / QSO Logger / Settings Buttons
Open the corresponding windows - see sections 3, 7, and 11.

---

## 3. Memory Channel Editor

### 3.1 Reading Channels
Click Read All Channels. Takes about 45 seconds on real hardware. Progress bar shows live progress.

### 3.2 The Channel Table
Shows Channel Number, Frequency, Mode, Name for every programmed channel. Uses a custom list display rather than a standard data grid due to a confirmed rendering bug in the underlying UI framework.

### 3.3 Writing Channels
Add Channel appends a blank row. Write All Channels pushes every row back to the radio.

### 3.4 Cancel
Active only during a Read or Write operation.

---

## 4. Activity Logging (CSV)

### 4.1 Starting a Logging Session
Click Start Logging. Creates a file under IcomRigControl/Logs/ inside your Documents folder.

Important: if your Documents folder is redirected by cloud sync (OneDrive on Windows, iCloud Drive on macOS if your user folder is under iCloud), the file lands in that synced Documents folder rather than a plain local one - check there first if a log file seems missing.

### 4.2 CSV Columns
Timestamp, FrequencyHz, Mode, SMeterS, SMeterDbm, RfPowerPercent, SwrRatio, AlcLevel, SupplyVoltage, CurrentDraw.

### 4.3 Stopping
Click Stop Logging.

---

## 5. Spectrum Waterfall Display

### 5.1 Reading the Display
Colors: black/dark blue = weak, progressing through blue, green, yellow, red as signal strength increases. Vertical streaks = real transmissions.

### 5.2 Frequency Axis Labels
Five frequency labels are shown above the waterfall, evenly spaced across the current span, and update live as you change frequency.

### 5.3 Click-to-Tune
Click anywhere on the waterfall to tune the radio to that frequency. The click position is mapped to a frequency based on the current center frequency and span.The server prints status to the console and runs until you press Ctrl+C.

### 9.3 Connecting a Desktop Client Remotely
On the connecting machine, open Settings, set Connection Mode to Remote, enter the server's host/IP, the same TCP port, and the same token used when starting the server. Save and restart the app.

### 9.4 Security Note
A connection token is always required - the server rejects any connection without the correct token, using a constant-time comparison to avoid leaking timing information. Never leave the token blank; the server will not run unauthenticated.

---

## 10. APRS Beacon

### 10.1 Setup
In Settings, under "APRS Beacon": enter your callsign, SSID (9 is conventional for mobile/HF APRS), symbol table/code, an optional comment, your latitude/longitude, and (on Windows) select an audio output device - this should be whatever audio interface is actually connected to your radio (its USB audio device, or an external soundcard wired to the mic/data port). Also set an Auto-Beacon Interval in minutes if you want automatic periodic beaconing (0 = manual only).

### 10.2 Sending a Beacon Manually
Click Send Beacon on the main dashboard. IcomRigControl builds an APRS position report, keys PTT, plays the generated AFSK tones through your selected audio device (or the system default on macOS), then releases PTT - guaranteed, even if something goes wrong mid-transmission, so the radio never gets stuck transmitting.

### 10.3 Automatic Periodic Beaconing
Click Auto Beacon to start automatic beaconing at the interval configured in Settings. Click it again to stop. The status text shows the current state and interval.

### 10.4 What's in the Beacon
Your configured comment, plus your radio's current live frequency and mode, are both included in every beacon's comment field automatically.

### 10.5 Technical Notes
Uses real-world HF APRS tones (1600/1800 Hz at 300 baud, matching what DireWolf and other HF APRS software actually use in practice) rather than the historical literal Bell 103 telephone-modem frequencies, which would not be decodable by real HF APRS listening stations.

### 10.6 Platform Support
Fully supported on both Windows (via NAudio/WASAPI, with output device selection) and macOS (via the built-in afplay command-line tool, using your systemwide default audio output device - macOS afplay has no device-selection capability of its own).

---

## 11. Settings Window Reference

Open via the Settings button on the main dashboard. Settings auto-close the window on successful save.

- **Radio Connection**: Demo/Serial/Remote mode, serial port, remote host/port/token.
- **APRS Beacon**: callsign, SSID, symbol, comment, position, audio device (Windows), auto-beacon interval.
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
Check for a cloud-sync-redirected path: on Windows, look for a OneDrive-synced Documents folder; on macOS, check whether your user folder or project is under iCloud Drive.

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

### 12.9 App Crashes Immediately When Opening Settings (macOS)
This was a real bug in an earlier version - a Settings component tried to use Windows-only audio APIs unconditionally. Fixed and confirmed working; if you see this on a current build, it indicates something has regressed and should be reported.

---

## 13. Field / EMCOMM Deployment Notes

- Resilient local QSO logging is deliberate EMCOMM-style redundancy - your log is never dependent on any external program being available.
- The headless server mode (section 9) is designed for exactly this kind of deployment: a Pi at the radio, reachable over 44Net/AMPRNet or a local mesh/VPN, with the operator's desktop app connecting remotely.
- Recommended deployment: Raspberry Pi 5, 8GB RAM.
- Use an A2-rated microSD card for sustained write performance.
- APRS beaconing works identically whether you're running on a Windows laptop or a MacBook in the field.

---

## 14. Revision History

2026-07-14: Initial manual created, covering Phases 1 through 8f.
2026-07-17: Full rewrite covering Phase 8's complete state, Phase 9, and Phase 10.
2026-07-18: Discovered the prior revision had been silently truncated (missing sections 1 through most of 9, both locally and on GitHub) - full rewrite from scratch to restore complete content, plus updates for Phase 10's macOS completion (afplay-based audio, confirmed working live) and the ARRL RTTY Roundup contest addition.
# IcomRigControl User Manual

**For:** Jim, KE4CON
**Covers:** Phases 1-8 (through Phase 8f, Direction 2)
**Last updated:** with each phase completion

---

## Table of Contents

1. Getting Started
2. The Main Dashboard
3. Memory Channel Editor
4. Activity Logging (CSV)
5. Spectrum Waterfall Display
6. EMMCOM Field Comms Server Integration
7. QSO Logging and Contest Mode
8. External Program Integration (N1MM / WSJT-X / HRD)
9. Troubleshooting
10. Field / EMCOMM Deployment Notes
11. Revision History

---

## 1. Getting Started

### 1.1 What IcomRigControl Does

IcomRigControl is a cross-platform desktop application (Windows, macOS, Linux, Raspberry Pi) for controlling an Icom IC-7300 or IC-7300MK2 over its CI-V interface via USB. Beyond basic rig control, it provides:

- Live meter monitoring (S-meter, power, SWR, ALC, voltage, current)
- A bulk memory channel editor
- CSV activity logging
- A live spectrum waterfall display
- Integration with an EMMCOM Field Comms Server
- ADIF-based QSO logging, including a simple built-in contest mode
- Automatic, resilient backup logging of QSOs from external programs (N1MM, WSJT-X, HRD Logbook)

### 1.2 System Requirements

- Windows, macOS, Linux desktop, or Raspberry Pi OS (ARM64)
- .NET 10 runtime
- An Icom IC-7300 or IC-7300MK2 connected via USB
- USB drivers for the radio installed

### 1.3 First Launch

1. Connect the IC-7300/MK2 to your computer via USB and power it on.
2. Launch IcomRigControl.
3. Note: as of this writing, the application defaults to a demo/simulated transport for development purposes. To connect to a real radio, the transport must be switched to SerialCivTransport with the correct serial port name (see Troubleshooting 9.1).
4. Once connected, the dashboard should show live frequency, mode, and meter data updating from the radio.

### 1.4 Radio Address Reference

IC-7300: CI-V address 0x94
IC-7300MK2: CI-V address 0xB6

---

## 2. The Main Dashboard

### 2.1 Status Row
Shows connection status text.

### 2.2 Frequency Entry
Type a frequency in Hz (e.g. 14074000) and click Set Freq to tune the radio.

### 2.3 Frequency Display
Large green digital readout of the current frequency. Mode buttons (LSB/USB/AM/CW/FM) change mode instantly.

### 2.4 Spectrum Scope / Waterfall
See section 5.

### 2.5 PTT Indicator
Red dot = transmitting, gray = receiving. Toggle PTT button manually keys/unkeys the radio.

### 2.6 Meters Grid
S-Meter, Power, SWR, ALC, Supply Voltage, Current Draw - all updating roughly twice per second.

### 2.7 Activity Logging Control
See section 4.

### 2.8 EMMCOM Control
See section 6.

### 2.9 Open Memory Editor Button
Opens the Memory Channel Editor window - see section 3.

---

## 3. Memory Channel Editor

### 3.1 Reading Channels
Click Read All Channels. Takes about 45 seconds on real hardware. Progress bar shows live progress.

### 3.2 The Channel Table
Shows Channel Number, Frequency, Mode, Name for every programmed channel. Uses a custom list display rather than a standard data grid due to a confirmed rendering bug in the underlying UI framework (see Troubleshooting 9.4).

### 3.3 Writing Channels
Add Channel appends a blank row. Write All Channels pushes every row back to the radio.

### 3.4 Cancel
Active only during a Read or Write operation.

---

## 4. Activity Logging (CSV)

### 4.1 Starting a Logging Session
Click Start Logging. Creates a file at:
[Documents folder]\\IcomRigControl\\Logs\\activity_YYYYMMDD_HHMMSS.csv

Important: if your Documents folder is redirected by OneDrive, the file lands in the OneDrive-synced Documents folder.

### 4.2 CSV Columns
Timestamp, FrequencyHz, Mode, SMeterS, SMeterDbm, RfPowerPercent, SwrRatio, AlcLevel, SupplyVoltage, CurrentDraw. One row roughly every 500ms.

### 4.3 Stopping
Click Stop Logging. File remains on disk.

---

## 5. Spectrum Waterfall Display

### 5.1 Reading the Display
Colors: black/dark blue = weak, progressing through blue, green, yellow, red as signal strength increases. Vertical streaks = real transmissions.

### 5.2 Known Limitations
No frequency axis labels yet. No click-to-tune yet.

---

## 6. EMMCOM Field Comms Server Integration

### 6.1 Setup
Enter server URL. Click the EMMCOM section Start button.

### 6.2 Status Indicator
Green = connected. Red = network error, app keeps retrying, never crashes.

### 6.3 Data Sent
Same MeterSnapshot structure as the CSV log, sent as JSON on every meter update.

---

## 7. QSO Logging and Contest Mode

Note: the underlying logging engine is complete and tested, but the dedicated logging UI panel has not yet been built.

### 7.1 How Logging Works
Every QSO logged is added to the session list AND immediately written through to a persistent ADIF file at:
[Documents folder]\\IcomRigControl\\Logs\\qsolog_YYYYMMDD_HHMMSS.adi

### 7.2 Auto-Fill
Frequency, mode, and band are captured automatically from the radio's current state at log time.

### 7.3 ADIF Export
Full session log can be exported to a standalone .adi file.

### 7.4 Contest Mode - ARRL Field Day
Exchange fields: Class and Section. Scoring: CW/FT8/FT4/RTTY = 2 points, phone = 1 point. Dupe checking: same station same band = dupe.

### 7.5 Adding New Contests
New contest definitions are a small, isolated code change (ContestCatalog.cs).

---

## 8. External Program Integration (N1MM / WSJT-X / HRD)

### 8.1 The Core Idea
IcomRigControl controls the radio. N1MM and HRD Logbook keep doing what they do best, while IcomRigControl keeps its own independent backup of every QSO logged anywhere.

### 8.2 Status
Built: IcomRigControl receiving QSOs from N1MM/WSJT-X/HRD.
Planned: sending rig status out; HRD direct-write bridge; LoTW; callsign lookup.

### 8.3 Configuring N1MM Logger+
Config menu, Configure Ports, Broadcast Data tab, check Contacts box, destination 127.0.0.1:port.

### 8.4 Configuring HRD Logbook
QSO Forwarding dialog, UDP Send, destination 127.0.0.1:port.

### 8.5 Configuring WSJT-X
File, Settings, Reporting, UDP Server, 127.0.0.1:2333.

### 8.6 Verifying It Works
Log a test QSO, check the session log file.

### 8.7 What Automatic Actually Requires
Listener running plus one-time config in each external program.

---

## 9. Troubleshooting

### 9.1 Finding Your Radio's Serial Port Name
Windows: Device Manager, Ports. macOS: ls /dev/tty.usb*

### 9.2 App Won't Connect to the Radio
Check power, cable, port conflicts, baud rate.

### 9.3 Documents Files Not Where Expected
Check the OneDrive-redirected path.

### 9.4 Memory Editor Table Looks Different
Intentional workaround for a grid rendering bug.

### 9.5 Waterfall Blank or Not Filling Panel
Known issue, fixed at the source.

### 9.6 EMMCOM or External Integration Shows Red/Error
Network endpoint unreachable, keeps retrying, never crashes.

---

## 10. Field / EMCOMM Deployment Notes

- Resilient local logging is deliberate EMCOMM-style redundancy.
- Recommended deployment: Raspberry Pi 5, 8GB RAM.
- Use an A2-rated microSD card for sustained write performance.

---

## 11. Revision History

2026-07-14: Initial manual created, covering Phases 1 through 8f (Direction 2: receiving external QSOs).

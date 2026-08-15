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
13. Running Alongside Other Programs (Sharing One Radio)
14. Field / EMCOMM Deployment Notes
15. Revision History

---

## 1. Getting Started

### 1.1 What IcomRigControl Does

IcomRigControl is a cross-platform desktop application (Windows, macOS, Linux, Raspberry Pi) for controlling an Icom IC-7300 or IC-7300MK2 over its CI-V interface, locally via USB or remotely over a network. Beyond basic rig control, it provides:

- Live meter monitoring (S-meter, power, SWR, ALC, voltage, current)
- A bulk memory channel editor
- CSV activity logging
- A live spectrum waterfall display with frequency axis labels and click-to-tune
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

### 2.8 APRS Beacon Controls
Send Beacon and Auto Beacon buttons. See section 10.

### 2.9 Integrations Status
Summary line showing which external integrations are currently active.

### 2.10 Open Memory Editor / QSO Logger / Settings Buttons
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

### 12.6 An External Integration Shows Red/Error
Network endpoint unreachable, keeps retrying, never crashes.

### 12.7 Settings Don't Seem to Take Effect
As of this version, Settings auto-closes and re-applies on save for most options. Connection Mode specifically requires a full app restart, since it determines how the radio connection is set up when the app starts.

### 12.8 Auto Beacon Won't Start
Check that a callsign is configured and the Auto-Beacon Interval in Settings is greater than 0.

### 12.9 App Crashes Immediately When Opening Settings (macOS)
This was a real bug in an earlier version - a Settings component tried to use Windows-only audio APIs unconditionally. Fixed and confirmed working; if you see this on a current build, it indicates something has regressed and should be reported.

---

## 13. Running Alongside Other Programs (Sharing One Radio)

Many operators want to run several programs on the same radio at the same time - for example Ham Radio Deluxe (HRD) for everyday logging, WSJT-X for FT8, and N1MM Logger+ for contests. This chapter explains, in plain language, what is and is not possible, and the easiest way to set it up. IcomRigControl plays nicely with all of them.

**In a nutshell:** Your radio has only one control cable, and only one program can use that cable at a time. To let several programs share it you either (a) let one program "own" the radio and have the others talk to the radio through that program, or (b) add a small helper that splits the one cable into several, or (c) use an IC-7300MK2, which has two control channels built in. Separately, *sharing your log* between programs needs none of this - that already works over the network. Pick one program to be the "boss" of the radio's dial, and you will avoid almost all trouble.

### 13.1 First, Two Different Things People Mean by "at the same time"

It helps a lot to separate two needs that get mixed up:

- **Controlling the radio** - reading and setting the frequency, the mode, and keying the transmitter (this is called CAT, short for Computer Aided Transceiver control; on Icom radios the protocol is called CI-V, short for Communications Interface, version 5). This is the hard one to share, and it is what the rest of this chapter is about.
- **Sharing your contacts (the log)** - getting a logged contact (a QSO) to appear in several logging programs. This is the *easy* one and needs no special tricks - see section 13.5.

### 13.2 Why Only One Program at a Time Can Control the Radio

Your radio connects to the computer as a single serial port (on Windows it has a name like `COM4`; on macOS or Linux it looks like `/dev/tty.usbserial-XXXX`). A serial port can be opened by only **one** program at a time. The moment HRD (for example) opens `COM4`, WSJT-X and N1MM will get an "access denied" or "port in use" error if they try to open the same `COM4`. So "three programs, one port" cannot work as-is. The three approaches below get around this.

### 13.3 The Three Ways to Share the Radio's Control Port

**Approach A - Let one program own the radio (easiest, recommended).**
One program opens the real serial port, and the others are told to use *that program* as their radio instead of the port. This is the least fuss and needs no extra software.
- The most common setup: **HRD** owns the radio. In **WSJT-X**, open **File > Settings > Radio**, and in the **Rig** dropdown choose **"Ham Radio Deluxe."** WSJT-X now controls the radio *through* HRD. Done - no virtual ports, no splitter.
- This works because HRD is built to act as a control server for other programs.

**Approach B - Use OmniRig as a shared control engine.**
OmniRig is a small, free helper that opens the radio once and lets several programs share it. **WSJT-X** and **N1MM Logger+** both support OmniRig (you select "OmniRig" as the radio). The catch: **HRD does not use OmniRig**, so this approach is best when the programs you want to combine are the ones that *do* support it (for example WSJT-X plus N1MM), not when HRD is in the mix.

**Approach C - Use com0com plus a CAT splitter.**
This is the "split one cable into several" method, and it is what you were thinking of.
- **com0com** is a free Windows tool that creates pairs of pretend ("virtual") serial ports that are wired to each other.
- **Important:** com0com by itself is not enough. It only makes the virtual ports; it does not read your radio and copy the data to them. You also need a small **splitter/hub** program that opens the real radio port and mirrors everything to the virtual ports, which HRD, WSJT-X, and N1MM then open instead of the real port.
- This works, but it is the most moving parts and the most to set up, so try Approach A or the MK2 (below) first.

**Approach D - Use an IC-7300MK2 (no sharing needed for two programs).**
The IC-7300MK2 provides **two independent control channels** at once. That means two programs can each have their own channel with no splitter and no virtual ports at all. (The original IC-7300 and the IC-705 have a single USB control port, so they need Approach A, B, or C.)

### 13.4 The One Rule That Prevents Most Problems

Even after the programs can all reach the radio, do **not** let two of them try to *change* the frequency, mode, or transmit at the same moment - they will fight, and you will see the dial jump around or the transmitter key unexpectedly. The rule: **pick one program to be the "boss" that controls the dial and transmits.** Let the others read the radio, or use them at different times (for example, run WSJT-X for an FT8 session, and N1MM only during a contest). You almost never actually need all three driving the radio in the same second.

### 13.5 Sharing Your Log Needs None of the Above

Getting your contacts into several programs is completely separate from the control-cable problem, and it is easy. Logging information travels over the local network as small broadcast messages (using UDP, short for User Datagram Protocol), and **any number of programs can listen at once.** IcomRigControl already sends and receives these (see the External Program Integration chapter). So if your real goal is "when I log a contact, I want it to show up in HRD and N1MM too," that works without com0com, without virtual ports, and without splitting the control cable at all.

Remember too that IcomRigControl keeps its **own** local log no matter what - that is the whole point of its backup-of-record design. Even if HRD, N1MM, or WSJT-X is closed, crashed, or unreachable, your contact is still saved in IcomRigControl.

### 13.6 Where IcomRigControl Fits

- IcomRigControl can be the "boss" that owns the radio, or it can be one of the participants alongside the others.
- Its **Receive-Only / TX-Inhibit** switch (the big red banner on the dashboard when it is on) is a separate coexistence tool: it blocks IcomRigControl from transmitting so another program can safely own transmit on a shared radio. That is about who gets to *key the transmitter*, not about the control cable.

### 13.7 Troubleshooting (Sharing One Radio)

| Symptom | What it means | What to do |
|---|---|---|
| A program says the port is "in use," "access denied," or "cannot open COM4" | Another program already has the radio's control port open | Close the other program, or set this program to use one of the sharing methods in 13.3 (Approach A is easiest) |
| The frequency dial jumps around on its own, or the radio keys unexpectedly | Two programs are both trying to control the radio at once | Pick one "boss" program (13.4); set the others to read-only or use them at different times |
| WSJT-X has no "Ham Radio Deluxe" option in its Rig list | HRD is not running, or its rig control is not started | Start HRD and its Radio Control first, then reopen WSJT-X's Radio settings |
| Contacts are not showing up in my other logger | This is a logging (UDP) setting, not a control-cable problem | Check the External Program Integration settings; make sure the other program is listening on the same address/port (usually `127.0.0.1`) |
| I set up com0com but nothing decodes/controls | com0com alone does not move data - the splitter/hub is missing | Add a CAT splitter program that reads the real radio and mirrors to the virtual ports (see Approach C), or switch to Approach A |

---

## 14. Field / EMCOMM Deployment Notes

- Resilient local QSO logging is deliberate EMCOMM-style redundancy - your log is never dependent on any external program being available.
- The headless server mode (section 9) is designed for exactly this kind of deployment: a Pi at the radio, reachable over 44Net/AMPRNet or a local mesh/VPN, with the operator's desktop app connecting remotely.
- Recommended deployment: Raspberry Pi 5, 8GB RAM.
- Use an A2-rated microSD card for sustained write performance.
- APRS beaconing works identically whether you're running on a Windows laptop or a MacBook in the field.

---

## 15. Revision History

2026-07-14: Initial manual created, covering Phases 1 through 8f.
2026-07-17: Full rewrite covering Phase 8's complete state, Phase 9, and Phase 10.
2026-08-15: Added Section 13, "Running Alongside Other Programs (Sharing One Radio)" - CAT/CI-V port-sharing options (own-the-radio, OmniRig, com0com + splitter, IC-7300MK2 dual ports) and the separate UDP log-sharing path.
2026-07-18: Discovered the prior revision had been silently truncated (missing sections 1 through most of 9, both locally and on GitHub) - full rewrite from scratch to restore complete content, plus updates for Phase 10's macOS completion (afplay-based audio, confirmed working live) and the ARRL RTTY Roundup contest addition.
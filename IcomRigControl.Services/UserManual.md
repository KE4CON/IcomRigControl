# IcomRigControl User Manual

**For:** Jim, KE4CON
**Covers:** Phases 1–8 (through Phase 8f, Direction 2)
**Last updated:** with each phase completion — see the bottom of this document for revision history.

---

## Table of Contents

1. [Getting Started](#1-getting-started)
2. [The Main Dashboard](#2-the-main-dashboard)
3. [Memory Channel Editor](#3-memory-channel-editor)
4. [Activity Logging (CSV)](#4-activity-logging-csv)
5. [Spectrum Waterfall Display](#5-spectrum-waterfall-display)
6. [EMMCOM Field Comms Server Integration](#6-emmcom-field-comms-server-integration)
7. [QSO Logging & Contest Mode](#7-qso-logging--contest-mode)
8. [External Program Integration (N1MM / WSJT-X / HRD)](#8-external-program-integration-n1mm--wsjt-x--hrd)
9. [Troubleshooting](#9-troubleshooting)
10. [Field / EMCOMM Deployment Notes](#10-field--emcomm-deployment-notes)
11. [Revision History](#11-revision-history)

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
- .NET 10 runtime (bundled with the app if distributed as self-contained; otherwise install separately)
- An Icom IC-7300 or IC-7300MK2 connected via USB
- USB drivers for the radio installed (Icom provides these; typically auto-installs as a virtual COM port / tty device)

### 1.3 First Launch

1. Connect the IC-7300/MK2 to your computer via USB and power it on.
2. Launch IcomRigControl.
3. **Note:** as of this writing, the application defaults to a demo/simulated transport for development purposes. To connect to a real radio, the transport must be switched to `SerialCivTransport` with the correct serial port name (see Troubleshooting §9.1 for how to find your port name). *This step will be simplified in a future update with a proper connection settings screen.*
4. Once connected, the dashboard should show live frequency, mode, and meter data updating from the radio.

### 1.4 Radio Address Reference

| Radio | CI-V Address |
|---|---|
| IC-7300 | 0x94 |
| IC-7300MK2 | 0xB6 |

The application must be told which radio model you're using, since the address differs.

---

## 2. The Main Dashboard

The main window is organized top to bottom as follows:

### 2.1 Status Row
Shows connection status text (e.g., "Connected (demo mode)" or "Connection failed").

### 2.2 Frequency Entry
- **Text box:** type a frequency in Hz (e.g., `14074000` for 14.074 MHz) and click **Set Freq** to tune the radio.

### 2.3 Frequency Display
- Large green digital readout of the current frequency, formatted with separators (e.g., `14.074.000`).
- Current mode shown below it (USB, LSB, CW, AM, FM).
- **Mode buttons:** click LSB / USB / AM / CW / FM to change mode instantly. Hovering highlights the button green.

### 2.4 Spectrum Scope / Waterfall
See §5 for full details. A live, scrolling color display of band activity around the current frequency.

### 2.5 PTT (Push-to-Talk) Indicator
- Colored dot: **red** = transmitting, **gray** = receiving.
- **Toggle PTT** button: manually keys/unkeys the radio.

### 2.6 Meters Grid
Six live meters updating roughly twice per second:

| Meter | Meaning |
|---|---|
| S-Meter | Received signal strength (e.g., S7, S9, S9+20dB) |
| Power | RF output power as a percentage |
| SWR | Standing Wave Ratio (e.g., 1.2:1) |
| ALC | Automatic Level Control percentage |
| Supply Voltage | DC voltage the radio is drawing (V) |
| Current Draw | DC current the radio is drawing (A) |

### 2.7 Activity Logging Control
See §4. A **Start Logging / Stop Logging** toggle with a status indicator.

### 2.8 EMMCOM Control
See §6. A URL entry field, status indicator, and Start/Stop toggle.

### 2.9 Open Memory Editor Button
Opens the separate Memory Channel Editor window — see §3.

---

## 3. Memory Channel Editor

Click **Open Memory Editor** on the main dashboard to open this window.

### 3.1 Reading Channels
Click **Read All Channels**. The app queries all 99 memory channels (this takes about 45 seconds on real hardware, since each channel requires a select-and-read round trip). A progress bar and text show live progress ("Reading channel 42 of 99...").

Channels that have never been programmed on the radio will not appear in the results table — only programmed channels show up.

### 3.2 The Channel Table
Displays Channel Number, Frequency (Hz), Mode, and Name for every programmed channel found.

**Note:** this table uses a custom list display rather than a standard data grid, due to a confirmed rendering bug in the underlying UI framework's grid control (see Troubleshooting §9.4 for background — this doesn't affect functionality, just how it was built).

### 3.3 Writing Channels
1. Click **Add Channel** to append a new blank row, or edit an existing row's values.
2. Click **Write All Channels** to push every row in the table back to the radio's memory. A progress bar shows write progress.

### 3.4 Cancel
The **Cancel** button is only active while a Read or Write operation is in progress, and stops it early.

---

## 4. Activity Logging (CSV)

This logs frequency, mode, and all six meter readings to a CSV file, timestamped, for later analysis (e.g., propagation studies, band activity over time).

### 4.1 Starting a Logging Session
Click **Start Logging** on the main dashboard. A new file is created immediately:
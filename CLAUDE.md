# IcomRigControl — Project Rules (CLAUDE.md)
## Project Identity
Name: IcomRigControl
Author: Jim, KE4CON
Language: C# (.NET 10)
UI Framework: Avalonia 11 (cross-platform desktop — macOS, Windows, Linux, Raspberry Pi)
Target Radios: Icom IC-7300 (address 94h) and IC-7300MK2 (address B6h)
Connection: USB serial via System.IO.Ports (115200 baud default); TCP/network mode
planned for v2
## Architecture Layers (never mix concerns across layers)
Layer 1 — CivEngine: Raw CI-V framing, serial port I/O, BCD encode/decode. No UI, no
radio model.
Layer 2 — RigModel: Transceiver class exposing clean C# properties and events. Consumes
CivEngine only.
Layer 3 — Services: Logger, EMMCOM bridge, APRS beacon, backfill queue, ADIF logger,
callsign lookup, LoTW sync, HRD bridge. Consume RigModel only.
Layer 4 — UI: Avalonia views and view-models. Consume Services and RigModel only. Never
touches CivEngine directly.
## Coding Standards
- C# 12 features are fine (.NET 10 target)
- async/await everywhere I/O is involved — no blocking calls on the UI thread
- CancellationToken passed through all async paths
- Events fired on the UI thread via Dispatcher.UIThread.InvokeAsync when updating
ObservableCollections
- Nullable reference types enabled — no suppression without a comment explaining why
- Records for immutable data (CivFrame, MeterReading, ConditionsSnapshot)
- No magic numbers — all CI-V command bytes defined as named constants in CivCommands.cs
- All serial port access goes through ICivTransport interface to allow mocking in tests
- Unit tests required for: BCD encode/decode, frame builder, frame parser, frequency
conversion
- For DataGrid-style tabular UI: prefer ItemsControl + DataTemplate over Avalonia.Controls.DataGrid.
  DataGrid 12.1.0 has a confirmed row-rendering bug (rows never appear despite correct,
  verified ItemsSource data) — reported upstream to AvaloniaUI/Avalonia.Controls.DataGrid.
- Environment.SpecialFolder.MyDocuments resolves to the OneDrive-redirected Documents path
  on this machine (C:\Users\jrosp\OneDrive\...\Documents), not plain C:\Users\jrosp\Documents.
  Always verify actual file output location when debugging file I/O — check both locations.
- Network-calling services (EmmcomBridge, future AprsBridge, callsign lookup, LoTW) must
  never throw back to the Transceiver's event dispatch — catch and record errors
  internally (LastError property), never crash the polling loop over a network hiccup.
- Neither IC-7300 nor IC-7300MK2 has a built-in TNC or APRS engine (unlike Icom's
  IC-9700/IC-2730, which do — but those use a different, radio-specific CI-V extension
  set not covered by this project). Any APRS from this project's target radios must be
  built as software AFSK/AX.25 packet audio and played out through an audio device into
  the radio's mic/data input, which is why APRS beaconing is merged with the audio phase.
- Custom-drawn UI controls (WaterfallControl and similar) sometimes need an explicit
  InvalidateVisual() call on the specific child element being redrawn (e.g. the Image),
  not just the parent UserControl — parent-only InvalidateVisual() can silently fail to
  repaint until an external event (window resize/move) forces it. Confirmed on Avalonia
  12.1.0 with WriteableBitmap-backed Image controls.
- Never reproduce copyrighted product photography (e.g. Icom press/dealer photos) as an
  asset in this project. The radio front-panel image for Phase 11 must be either a photo
  the user personally took of their own hardware, a licensed/permitted image, or an
  original vector illustration — not a photo sourced from the web.
- HRD Logbook's SQLite database schema (table TABLE_HRD_CONTACTS_V01, columns like
  col_call/col_time_on/col_mode/col_band) is reverse-engineered from community sources,
  not officially published by HRD — it can change without notice on any HRD update.
  Any direct-write HRD integration (HrdSqliteBridge) must be a best-effort bonus layered
  on top of the always-reliable ADIF export path, never a replacement for it, and must
  fail silently/log-only if the schema doesn't match what's expected (never corrupt or
  crash HRD's database).
## Feature Priorities (build in this order)
Phase 1: CI-V engine + serial connection + frequency read/set + mode read/set — COMPLETE (BcdCodec, CivCommands, CivFrame, CivFrameBuilder, CivFrameParser, ICivTransport, SerialCivTransport, 23 passing tests)
Phase 2: Meter polling (S-meter, SWR, ALC, power, voltage, current) — COMPLETE (MeterDecoder, RadioModel, MeterSnapshot, Transceiver with async polling loop and mode/frequency/PTT event wiring, 43 passing tests)
Phase 3: Avalonia UI — main panel with frequency display, mode selector, meter gauges — COMPLETE (live dashboard: frequency entry + display, mode selector buttons with hover styling, PTT toggle + indicator, all six meters live-updating via DemoCivTransport; MainWindowViewModel fully wired to Transceiver)
Phase 4: Memory bulk editor (read all 99 channels, edit in DataGrid, write back) — COMPLETE (MemoryChannel record, CI-V memory select/read commands, Transceiver.ReadAllMemoriesAsync/WriteMemoryChannelAsync using TaskCompletionSource-based response correlation to avoid event-subscription race conditions, MemoryEditorViewModel + MemoryEditorWindow using ItemsControl table — see DataGrid note above, 52 passing tests)
Phase 5: Activity logger (CSV output, frequency/mode/meter timestamped) — COMPLETE (ActivityLogger service in IcomRigControl.Services, subscribes to Transceiver.MeterUpdated, writes timestamped CSV per logging session; Start/Stop toggle button in MainWindow with live status indicator; 56 passing tests)
Phase 6: EMMCOM dashboard integration (push rig status to Field Comms Server) — COMPLETE (EmmcomBridge service posts MeterSnapshot as JSON to a configurable HTTP endpoint on every MeterUpdated event; Start/Stop toggle + URL entry box + status indicator in MainWindow; network failures caught and surfaced via LastError, never crash polling; 60 passing tests)
Phase 7: Spectrum scope capture and waterfall display — CORE COMPLETE (ScopeDataDecoder, CivFrameBuilder scope commands on/off/span/mode/waveform-output, Transceiver.StartScopeAsync/StopScope with WaveformUpdated event, WaterfallControl using WriteableBitmap with black->blue->green->yellow->red gradient, DemoCivTransport generates fixed-position synthetic signals so streaks render realistically; 74 passing tests). REMAINING: frequency axis labels above the waterfall; click-to-tune (click a point on the waterfall to jump the radio to that frequency).
Phase 8: ADIF logging (general + contest + callsign lookup + LoTW + HRD integration) — ACTIVE.
  8a. Core logging — COMPLETE: QsoRecord model (callsign, freq, mode, band, date/time,
  RST sent/received, contest exchange fields, serial number); AdifWriter service
  producing standard ADIF-tagged export files any logging program can import (header,
  per-QSO formatting, optional-field omission); QsoLogger service managing the session's
  in-memory QSO list with auto-fill of frequency/mode/band from the live Transceiver at
  log time, 8-band frequency-to-band mapping (160M-70CM), callsign uppercasing, ADIF
  export, and log clearing. 97 passing tests. REMAINING: logging UI panel (quick-entry
  fields, Log QSO button, running table).
  8b. Contest mode — ACTIVE: ContestDefinition record (exchange field labels, scoring
  rules, valid bands/modes, dupe rules) + small built-in catalog starting with ARRL
  Field Day (fully modeled scoring including power class and bonus points) before adding
  further contests incrementally. Dupe-checking (same station/band = dupe) and a live
  running score display.
  8c. Callsign lookup: ICallsignLookupSource interface with multiple pluggable
  implementations — QrzLookupSource (requires paid QRZ XML Data subscription, user
  supplies their own credentials), HamQthLookupSource (free, no subscription),
  CallookLookupSource (free, US calls only, no signup). User selects their preferred
  source in Settings (radio button or dropdown) — if they already pay for QRZ they can
  use it, otherwise a free source works with no setup. Auto-fills name/grid/location
  fields when a callsign is typed into the log entry. Never blocks manual entry if
  lookup fails or is unavailable (offline, source down, no subscription) — same
  never-crash-never-block network pattern as EmmcomBridge.
  8d. LoTW upload/download: relies on the user having ARRL's free TQSL tool already
  installed and their station certificate already configured (one-time setup outside
  this app, ARRL requirement). LotwBridge service shells out to TQSL as an external
  process to sign the ADIF export (do not reimplement ARRL's certificate/signing logic
  in-house — TQSL is the correct, ARRL-sanctioned tool for this). Upload = signed .tq8
  POST to ARRL's LoTW server. Download = query LoTW's API for QSOs confirmed since a
  given date, returned as ADIF, matched back against local QsoRecords to mark confirmed.
  8e. Ham Radio Deluxe integration (two layers, in order):
    Layer 1 (primary, reliable) — ADIF handoff. HRD Logbook v6.9+ natively imports/
    exports ADIF against its SQLite backend (confirmed working path, actively
    maintained by HRD). The existing AdifWriter output should import directly with no
    new code — verify once near a machine with HRD installed.
    Layer 2 (bonus, best-effort) — HrdSqliteBridge direct write. HRD Logbook v6.9+
    replaced Access with an embedded SQLite database (default location on Windows:
    %AppData%\Simon Brown, HB9DRV\HRD Logbook\), table TABLE_HRD_CONTACTS_V01 with
    columns including col_call, col_time_on, col_mode, col_band, col_country,
    col_contest_id (schema reverse-engineered from community sources, not officially
    documented — see coding standards note above on required defensive handling). Uses
    Microsoft.Data.Sqlite (lightweight, no server process) to write each logged QSO
    directly into HRD's live database as it's logged in IcomRigControl, so it appears
    in HRD Logbook without a manual export/import step. Toggleable in Settings, off by
    default, always falls back gracefully to ADIF-only if the database file or expected
    schema isn't found.
    Longer-term alternative worth revisiting once Phase 9 (networking) exists: expose
    IcomRigControl's own rig-status over a simple TCP/CAT-style interface so HRD's other
    components (or third-party tools) can query it directly as a rig-control source,
    rather than IcomRigControl reaching into HRD's database at all — more sustainable
    than schema reverse-engineering long-term.
Phase 9: Remote/network mode (headless Pi server + TCP client)
Phase 10: Remote audio + APRS beacon (combined) — NAudio on Windows, AVFoundation
wrapper on macOS, for playing software-generated AFSK/AX.25 APRS packet audio out
through an audio device into the radio's mic/data input (HF APRS on IC-7300/MK2); also
covers general remote-audio RX/TX streaming. Beacon target: APRS-Command (formerly
CrossPlatformAPRS) — bridge mechanism (UDP/file/direct) to be determined once
APRS-Command's ingestion method is reviewed.
Phase 11: Clickable radio front-panel control — a real (user-photographed) or vector
image of the IC-7300/MK2 front panel with transparent clickable regions overlaid, each
wired to the corresponding existing CI-V command (mode, VFO, band up/down, tuning).
Start with a small subset (tuning area, mode buttons, band up/down) to prove the
click-to-command pipeline before mapping the full panel. See copyright note above —
image source must be the user's own photo, a licensed image, or an original
illustration, never a scraped web photo. One shared image may cover both radio models
if the front panel layout is identical; separate images only if the MK2's panel differs.
## What NOT to do
- Do not implement features out of phase order without explicit instruction
- Do not add NuGet packages without listing them here first
- Do not put CI-V logic in ViewModels
- Do not put UI code in CivEngine or RigModel
- Do not use Thread.Sleep — use Task.Delay with CancellationToken
- Do not swallow exceptions silently — log and surface them
- Do not hardcode radio addresses — read from config
- Do not store QRZ/HamQTH/LoTW credentials in plain text in source-controlled files —
  use a local config file excluded via .gitignore, or the OS credential store.
- Do not write to HRD Logbook's SQLite database without defensive schema checks — a
  broken assumption here could corrupt another program's live data file.
## Radio Addresses
IC-7300: 0x94 (controller default: 0xE0)
IC-7300MK2: 0xB6 (controller default: 0xE0)
Both use the same CI-V command set with minor MK2 additions (see Appendix A of master
reference)
## Key References
- IC-7300MK2 CI-V Reference Guide (official Icom PDF):
icomuk.co.uk/files/icom/PDF/productAdditionalFile/IC-7300MK2_ENG_CI-V_0.pdf
- wfview source (CI-V implementation reference): github.com/wf-group/wfview
- This master reference PDF: IcomRigControl_Master_Reference.pdf (project docs folder)
- ADIF specification: adif.org (format spec for Phase 8 export files)
- QRZ.com XML API docs (Phase 8c, requires paid subscription): xml.qrz.com
- HamQTH XML API docs (Phase 8c, free): hamqth.com/developers.php
- Callook.us API (Phase 8c, free, US calls only): callook.info
- ARRL LoTW / TQSL (Phase 8d): lotw.arrl.org
- HRD Logbook database docs (Phase 8e): support.hamradiodeluxe.com — SQLite backend as
  of v6.9 (Oct 2025 rewrite); table/column names not officially documented, sourced from
  community references only.
## Related Projects
- APRS-Command (formerly CrossPlatformAPRS, KE4CON): APRS beacon target for Phase 10
  (combined audio/APRS phase) — project was archived and renamed; ingestion mechanism
  (UDP/file/etc.) not yet reviewed against this project.
- EMMCOM Field Comms Server: dashboard integration target for Phase 6 — COMPLETE, real
  endpoint URL to be confirmed and configured when available
- Ham Radio Deluxe (Simon Brown, HB9DRV): user's existing logging tool of choice, being
  integrated with per Phase 8e rather than replaced — IcomRigControl's rig control is
  intended to be preferred over HRD's own rig control, while HRD Logbook's logging
  features remain in active use.
## Session Start Checklist
Before writing any code in a session:
1. Read this file
2. Confirm which Phase is active
3. Check that the layer being touched matches the Phase
4. Do not refactor other layers unless the current Phase explicitly requires it
## Deployment Targets
Headless CI-V server (Phase 9, no UI): Raspberry Pi 4 or 5, 2GB minimum, 4GB comfortable.
Full Avalonia UI + scope on Pi (Phase 7-9 combined): Raspberry Pi 5, 8GB RAM — standardized target for breathing room with scope waterfall, EMMCOM bridge, and APRS beacon running concurrently.
Storage: 16-32GB microSD (A2 rated recommended for sustained write performance from ActivityLogger).

## Supported Desktop Platforms (all four, via Avalonia 11)
Windows: primary development platform as of Phase 3 — VS Code + GitHub Desktop
macOS: fully supported secondary/testing platform (MacBook Pro M1 Pro, macOS 26+)
Linux desktop: full support (Ubuntu/Debian primary test targets)
Raspberry Pi OS (Linux ARM64): Pi 5, 8GB — see Deployment Targets above
All four platforms build from the same Avalonia 11 UI project — no platform-specific UI code unless a capability is genuinely unavailable, in which case isolate behind ICivTransport per architecture rules.
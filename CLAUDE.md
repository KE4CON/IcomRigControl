# IcomRigControl — Project Rules (CLAUDE.md)
## Project Identity
Name: IcomRigControl
Author: Jim, KE4CON
Language: C# (.NET 10)
UI Framework: Avalonia 11 (cross-platform desktop — macOS, Windows, Linux, Raspberry Pi)
Target Radios: Icom IC-7300 (address 94h) and IC-7300MK2 (address B6h)
Connection: USB serial via System.IO.Ports (115200 baud default); TCP/network mode
planned for v2
## Core Design Principle: Resilience / Backup-of-Record (EMCOMM discipline)
IcomRigControl's own QsoLogger is the resilient backup of record for all logged QSOs,
independent of whether HRD Logbook, N1MM, or any other external program is running,
reachable, or healthy. Every QSO logged anywhere in this ecosystem (directly in
IcomRigControl, or mirrored in from N1MM/WSJT-X via Phase 8f Direction 2) must land in
IcomRigControl's own persistent local log. Integrations with HRD, N1MM, LoTW, etc. are
one-way, best-effort *additions* on top of this local log — never a dependency the local
log needs to function, and never a gate that can cause a QSO to go unrecorded if the
external program is down. This mirrors the user's EMCOMM "always have a backup plan"
principle, applied to logging infrastructure.
## Architecture Layers (never mix concerns across layers)
Layer 1 — CivEngine: Raw CI-V framing, serial port I/O, BCD encode/decode. No UI, no
radio model.
Layer 2 — RigModel: Transceiver class exposing clean C# properties and events. Consumes
CivEngine only.
Layer 3 — Services: Logger, EMMCOM bridge, APRS beacon, backfill queue, ADIF logger,
callsign lookup, LoTW sync, HRD bridge, RadioInfo UDP broadcaster, N1MM/WSJT-X UDP
listener. Consume RigModel only.
Layer 4 — UI: Avalonia views and view-models. Consume Services and RigModel only. Never
touches CivEngine directly.
## Documentation Requirements
Two documents must be kept current, together, as a single discipline:
- CLAUDE.md (this file) — the developer/AI-facing project rules and phase status.
- UserManual.md (project root) — the end-user-facing manual covering everything a
  completed phase adds or changes for the person actually using the app (new screens,
  buttons, file locations, external-program configuration steps, troubleshooting notes).
Whenever a phase (or sub-phase, e.g. 8a/8b/8f) is marked COMPLETE in the Feature
Priorities section below, UserManual.md MUST be updated in the same commit to document
what that phase added from the user's perspective — not just noted as done in CLAUDE.md.
Do not consider a phase's documentation finished until both files reflect it. UserManual.md
has no automatic "read at session start" mechanism the way CLAUDE.md does for Claude
tools — this rule is what keeps it current in place of that automation, so treat it as a
non-optional step of phase completion, not an occasional nice-to-have.
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
- Network-calling services (EmmcomBridge, future AprsBridge, callsign lookup, LoTW,
  RadioInfo broadcaster, N1MM/WSJT-X UDP listener) must never throw back to the
  Transceiver's event dispatch — catch and record errors internally (LastError property),
  never crash the polling loop over a network hiccup.
- QsoLogger must write through to a persistent local ADIF file as each QSO is logged
  (not just held in memory until a manual export) — same "session file created on start,
  appended as events occur" pattern as ActivityLogger — so a crash of IcomRigControl
  itself does not lose an in-progress logging session. COMPLETE — see Core Design
  Principle above.
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
- N1MM/WSJT-X/HRD UDP integration (Phase 8f) uses a genuinely public, documented
  XML-over-UDP protocol (N1MM's External UDP Messages, default port 12060; WSJT-X shares
  the same packet family on port 2333; HRD Logbook has its own "UDP Receive" feature that
  consumes this same protocol) — unlike the HRD SQLite schema, this is stable and does
  not carry the same "could break silently" risk. Prefer this integration style over
  private-schema reverse engineering wherever a documented protocol option exists. Build
  the RadioInfo broadcaster once, generically, with a configurable list of destination
  IP:port targets — the same broadcaster serves HRD's UDP Receive listener, N1MM's
  RadioInfo consumer, and any other program on this protocol, simultaneously.
- Do not attempt to make N1MM or HRD's own rig control literally driven by
  IcomRigControl (replacing their CAT connection to the radio) as part of Phase 8 — that
  requires a virtual-serial-port or network-CAT-interface mechanism, which is Phase 9
  (Remote/network mode) territory. Phase 8's UDP broadcaster is supplementary
  status-sharing, not a CAT replacement; revisit true CAT replacement explicitly, if
  wanted, once Phase 9 exists.
## Feature Priorities (build in this order)
Phase 1: CI-V engine + serial connection + frequency read/set + mode read/set — COMPLETE (BcdCodec, CivCommands, CivFrame, CivFrameBuilder, CivFrameParser, ICivTransport, SerialCivTransport, 23 passing tests)
Phase 2: Meter polling (S-meter, SWR, ALC, power, voltage, current) — COMPLETE (MeterDecoder, RadioModel, MeterSnapshot, Transceiver with async polling loop and mode/frequency/PTT event wiring, 43 passing tests)
Phase 3: Avalonia UI — main panel with frequency display, mode selector, meter gauges — COMPLETE (live dashboard: frequency entry + display, mode selector buttons with hover styling, PTT toggle + indicator, all six meters live-updating via DemoCivTransport; MainWindowViewModel fully wired to Transceiver)
Phase 4: Memory bulk editor (read all 99 channels, edit in DataGrid, write back) — COMPLETE (MemoryChannel record, CI-V memory select/read commands, Transceiver.ReadAllMemoriesAsync/WriteMemoryChannelAsync using TaskCompletionSource-based response correlation to avoid event-subscription race conditions, MemoryEditorViewModel + MemoryEditorWindow using ItemsControl table — see DataGrid note above, 52 passing tests)
Phase 5: Activity logger (CSV output, frequency/mode/meter timestamped) — COMPLETE (ActivityLogger service in IcomRigControl.Services, subscribes to Transceiver.MeterUpdated, writes timestamped CSV per logging session; Start/Stop toggle button in MainWindow with live status indicator; 56 passing tests)
Phase 6: EMMCOM dashboard integration (push rig status to Field Comms Server) — COMPLETE (EmmcomBridge service posts MeterSnapshot as JSON to a configurable HTTP endpoint on every MeterUpdated event; Start/Stop toggle + URL entry box + status indicator in MainWindow; network failures caught and surfaced via LastError, never crash polling; 60 passing tests)
Phase 7: Spectrum scope capture and waterfall display — CORE COMPLETE (ScopeDataDecoder, CivFrameBuilder scope commands on/off/span/mode/waveform-output, Transceiver.StartScopeAsync/StopScope with WaveformUpdated event, WaterfallControl using WriteableBitmap with black->blue->green->yellow->red gradient, DemoCivTransport generates fixed-position synthetic signals so streaks render realistically; 74 passing tests). REMAINING: frequency axis labels above the waterfall; click-to-tune (click a point on the waterfall to jump the radio to that frequency).
Phase 8: ADIF logging (general + contest + callsign lookup + LoTW + HRD + N1MM/WSJT-X) — ACTIVE.
  8a. Core logging — COMPLETE: QsoRecord model (callsign, freq, mode, band, date/time,
  RST sent/received, contest exchange fields, serial number); AdifWriter service
  producing standard ADIF-tagged export files any logging program can import (header,
  per-QSO formatting, optional-field omission); QsoLogger service managing the session's
  in-memory QSO list with auto-fill of frequency/mode/band from the live Transceiver at
  log time, 8-band frequency-to-band mapping (160M-70CM), callsign uppercasing, ADIF
  export, log clearing, AND persistent write-through to a timestamped session file on
  every LogQso call (resilient backup, see Core Design Principle). 118 passing tests.
  REMAINING: logging UI panel (quick-entry fields, Log QSO button, running table).
  8b. Contest mode — COMPLETE: ContestDefinition record (exchange field labels, scoring
  rules, valid bands/modes, dupe rules) + ContestCatalog with ARRL Field Day (points by
  mode: CW/FT8/FT4/RTTY=2, phone=1; same-station-same-band dupe rule); ContestScoreCalculator
  computes running totals (points, QSO count, sections worked as multiplier candidates).
  Primary use case: casual/simple contests logged directly in IcomRigControl. For rare or
  frequently-changing contests, the user's workflow is to run N1MM instead (which owns
  contest-rule currency) — see 8f for how those QSOs still reach this project's log.
  REMAINING: additional contest catalog entries beyond Field Day, added incrementally as
  needed; live running score display in UI.
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
  8e. Ham Radio Deluxe integration (three layers, in order). User's stated workflow: HRD
  Logbook remains the primary day-to-day logger and full HRD suite (bandmap, DX cluster,
  awards tracking) stays in active use; IcomRigControl is the preferred radio controller
  feeding HRD accurate status; IcomRigControl's own log remains the resilient backup of
  record per the Core Design Principle above regardless of HRD's availability.
    Layer 1 (status feed, real-time) — the shared RadioInfoUdpBroadcaster (see 8f)
    pointed at HRD Logbook's documented "UDP Receive" feature, so HRD's logbook fields
    and other components auto-populate from IcomRigControl's rig control without HRD
    needing its own CAT connection to the radio. NOT YET BUILT.
    Layer 2 (primary logging bridge, reliable) — ADIF handoff. HRD Logbook v6.9+
    natively imports/exports ADIF against its SQLite backend (confirmed working path,
    actively maintained by HRD). The existing AdifWriter output should import directly
    with no new code — verify once near a machine with HRD installed.
    Layer 3 (bonus, best-effort) — HrdSqliteBridge direct write. HRD Logbook v6.9+
    replaced Access with an embedded SQLite database (default location on Windows:
    %AppData%\Simon Brown, HB9DRV\HRD Logbook\), table TABLE_HRD_CONTACTS_V01 with
    columns including col_call, col_time_on, col_mode, col_band, col_country,
    col_contest_id (schema reverse-engineered from community sources, not officially
    documented — see coding standards note above on required defensive handling). Uses
    Microsoft.Data.Sqlite (lightweight, no server process) to write each logged QSO
    directly into HRD's live database as it's logged in IcomRigControl. Toggleable in
    Settings, off by default, always falls back gracefully to ADIF-only if the database
    file or expected schema isn't found. NOT YET BUILT.
  8f. N1MM Logger+, WSJT-X, and HRD UDP integration — the shared RadioInfoUdpBroadcaster
  and listener infrastructure. User's stated workflow: for rare/frequently-changing
  contests, run N1MM (which owns contest-rule currency) with IcomRigControl as the radio
  controller feeding it status; N1MM's logged contacts flow back into IcomRigControl's
  resilient local log. Two directions:
    Direction 1 (send) — NOT YET BUILT. RadioInfoUdpBroadcaster will send RadioInfo-format
    XML packets (frequency, mode, band, TX/RX state) derived from Transceiver's live
    state, matching the documented RadioInfo packet schema shared by N1MM, WSJT-X, and
    HRD's UDP Receive feature. To be built once, generically, with a configurable list
    of destination IP:port targets so it can feed N1MM, HRD, and/or WSJT-X
    simultaneously — not three separate broadcasters.
    Direction 2 (receive) — COMPLETE: ContactPacketParser parses Contact-format XML
    packets (9 passing tests) into QsoRecord; ContactUdpListener listens on a
    configurable UDP port, receives real packets via UdpClient (proven with an actual
    loopback socket send/receive integration test, not just mocked), and mirrors valid
    contacts into QsoLogger via the new LogReceivedQso method (bypasses local-radio
    auto-fill since the received record already carries its own correct
    frequency/mode/timestamp from the sending program). Malformed packets are silently
    ignored without crashing the listener. 4 passing tests. This is the piece that
    fulfills the Core Design Principle for externally-logged QSOs — N1MM/WSJT-X/HRD
    contacts now flow automatically into IcomRigControl's resilient local log once the
    external program's own UDP broadcast is configured to point at this listener (a
    one-time setup step in each external program, not a per-session step). REMAINING:
    expose Start/Stop toggle and port configuration in the UI (currently only
    instantiable in code); update UserManual.md §8 once exposed in UI.
    Both directions use plain UdpClient send/receive, XML parsing via System.Xml, and
    follow the same never-crash-on-network-hiccup pattern as EmmcomBridge.
Phase 9: Remote/network mode (headless Pi server + TCP client) — also the right place to
revisit true CAT-replacement for N1MM/HRD (virtual serial port or network CAT interface)
if wanted, once this phase's networking foundation exists — see coding standards note
above on why this is explicitly out of scope for Phase 8.
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
- Do not broadcast N1MM/WSJT-X/HRD UDP traffic to anything other than 127.0.0.1 or an
  explicitly user-configured LAN address — never broadcast to wider subnets or the
  internet by default.
- Do not make any external integration (HRD, N1MM, WSJT-X, LoTW, EMMCOM) a dependency
  that could prevent a QSO from being recorded in IcomRigControl's own local log — see
  Core Design Principle above.
- Do not mark a phase COMPLETE in this file without also updating UserManual.md in the
  same commit — see Documentation Requirements above.
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
  community references only. HRD's "UDP Receive" / QSO Forwarding feature documented at
  the same support site.
- N1MM External UDP Messages docs (Phase 8f): n1mmwp.hamdocs.com/appendices/external-udp-broadcasts/
  — full XML schema for RadioInfo, Contact, ContactInfo, Spot Data, Score Reporting, etc.
- UserManual.md (project root): end-user-facing documentation — see Documentation
  Requirements above for the maintenance rule.
## Related Projects
- APRS-Command (formerly CrossPlatformAPRS, KE4CON): APRS beacon target for Phase 10
  (combined audio/APRS phase) — project was archived and renamed; ingestion mechanism
  (UDP/file/etc.) not yet reviewed against this project.
- EMMCOM Field Comms Server: dashboard integration target for Phase 6 — COMPLETE, real
  endpoint URL to be confirmed and configured when available
- Ham Radio Deluxe (Simon Brown, HB9DRV): user's PRIMARY day-to-day logger and full
  suite (bandmap, DX cluster, awards tracking) remain in active use — not being replaced.
  IcomRigControl's role is (1) preferred radio controller feeding HRD accurate status via
  UDP, (2) resilient independent backup log per Core Design Principle, (3) optional
  direct-write convenience bridge. See Phase 8e.
- N1MM Logger+: user's preferred tool for rare/frequently-changing contests specifically
  because N1MM owns contest-rule currency — IcomRigControl's simple built-in contest mode
  (8b) is for casual/simple contests only, by design, not a competitor to N1MM's catalog.
  IcomRigControl is the radio controller feeding N1MM status; N1MM's logged contacts flow
  back into IcomRigControl's resilient local log. See Phase 8f — receive direction COMPLETE.
- WSJT-X: shares the same UDP protocol family as N1MM (Phase 8f), covered by the same
  integration work with a different default port.
## Session Start Checklist
Before writing any code in a session:
1. Read this file
2. Confirm which Phase is active
3. Check that the layer being touched matches the Phase
4. Do not refactor other layers unless the current Phase explicitly requires it
5. When completing a phase this session, update UserManual.md in the same commit — see
   Documentation Requirements above. This step is not optional.
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
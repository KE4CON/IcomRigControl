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
reachable, or healthy. Every QSO logged anywhere in this ecosystem must land in
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
callsign lookup (Callook/QRZ/HamQTH), LoTW bridge (via TQSL), HRD bridge, RadioInfo UDP
broadcaster, N1MM/WSJT-X UDP listener. Consume RigModel only.
Layer 4 — UI: Avalonia views and view-models. Consume Services and RigModel only. Never
touches CivEngine directly.
## Documentation Requirements
Two documents must be kept current, together, as a single discipline:
- CLAUDE.md (this file) — the developer/AI-facing project rules and phase status.
- UserManual.md (project root) — the end-user-facing manual covering everything a
  completed phase adds or changes for the person actually using the app.
Whenever a phase (or sub-phase) is marked COMPLETE in Feature Priorities below,
UserManual.md MUST be updated in the same commit.
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
- Environment.SpecialFolder.MyDocuments resolves to the OneDrive-redirected Documents path
  on this machine, not plain C:\Users\jrosp\Documents. Always verify actual file output
  location when debugging file I/O.
- Network-calling services must never throw back to the Transceiver's event dispatch —
  catch and record errors internally, never crash the polling loop.
- QsoLogger writes through to a persistent local ADIF file as each QSO is logged.
  COMPLETE — see Core Design Principle above.
- Neither IC-7300 nor IC-7300MK2 has a built-in TNC or APRS engine. APRS must be built as
  software AFSK/AX.25 packet audio played out through an audio device, hence merged with
  the audio phase.
- Custom-drawn UI controls sometimes need an explicit InvalidateVisual() call on the
  specific child element (e.g. the Image), not just the parent UserControl.
- Never reproduce copyrighted product photography as an asset in this project.
- HRD Logbook's SQLite schema is reverse-engineered from community sources — any
  direct-write integration must be best-effort, defensive, never a replacement for ADIF.
- N1MM/WSJT-X/HRD UDP integration uses a public, documented XML-over-UDP protocol. One
  shared broadcaster/listener pair serves all three.
- Do not attempt to make N1MM or HRD's own rig control literally driven by
  IcomRigControl (CAT replacement) as part of Phase 8 — that's Phase 9 territory.
- ICallsignLookupSource implementations must NEVER throw. QRZ and HamQTH both use
  session-based login (login once, cache, reuse, re-login only on session error).
- LoTW signing MUST be delegated to ARRL's own TQSL tool via the ITqslProcessRunner
  abstraction (TqslProcessRunner shells out to the real tqsl executable as an external
  process) — this project must never reimplement ARRL's certificate/signing logic
  in-house. LotwBridge itself never throws: signing failures, missing files, and network
  errors are all returned as a non-success result with an explanatory message, at every
  step of both upload and download.
## Feature Priorities (build in this order)
Phase 1: CI-V engine + serial connection + frequency read/set + mode read/set — COMPLETE (23 passing tests)
Phase 2: Meter polling — COMPLETE (43 passing tests)
Phase 3: Avalonia UI — main panel — COMPLETE
Phase 4: Memory bulk editor — COMPLETE (52 passing tests)
Phase 5: Activity logger (CSV) — COMPLETE (56 passing tests)
Phase 6: EMMCOM dashboard integration — COMPLETE (60 passing tests)
Phase 7: Spectrum scope capture and waterfall display — CORE COMPLETE (74 passing tests). REMAINING: frequency axis labels; click-to-tune.
Phase 8: ADIF logging (general + contest + callsign lookup + LoTW + HRD + N1MM/WSJT-X) — ACTIVE.
  8a. Core logging — COMPLETE (118 passing tests). REMAINING: logging UI panel.
  8b. Contest mode — COMPLETE (Field Day). REMAINING: additional contests; live score UI.
  8c. Callsign lookup — COMPLETE: all three planned sources (Callook, QRZ, HamQTH) built
  and tested against each service's real documented API. 20 passing tests. REMAINING:
  user-facing source selection in Settings; wiring into the QSO logging UI panel.
  8d. LoTW upload/download — COMPLETE: ITqslProcessRunner abstraction (TqslProcessRunner
  shells out to the real tqsl executable, never reimplementing ARRL's signing logic) +
  LotwBridge orchestrating sign-then-upload and query-then-parse-download flows. Upload
  path: TQSL signs the ADIF export to a .tq8, which is then POSTed to ARRL's LoTW upload
  endpoint. Download path: queries LoTW's report endpoint for QSOs confirmed since a
  given date, parses the returned ADIF text with a minimal tag-scanning parser (LoTW's
  output is well-formed per spec, so a full ADIF library isn't needed) into QsoRecords.
  Every failure point (missing ADIF file, TQSL not found/failed, network error, HTTP
  failure) returns a clear non-success result rather than throwing. 6 passing tests.
  REMAINING: UI for configuring the TQSL executable path and triggering upload/download;
  matching downloaded confirmations back against local QsoRecords to mark them confirmed
  (currently DownloadConfirmedQsosAsync returns the records but nothing consumes them
  yet to update local log entries).
  8e. Ham Radio Deluxe integration (three layers) — NOT YET BUILT. Layer 1 (status feed)
  ready to wire up now that RadioInfoUdpBroadcaster exists (8f).
  8f. N1MM Logger+, WSJT-X, and HRD UDP integration — COMPLETE (both directions, 137
  passing tests). REMAINING: expose Start/Stop toggles and destination/port
  configuration in the UI.
163 passing tests total across the whole project as of Phase 8d completion.
Phase 9: Remote/network mode (headless Pi server + TCP client) — also the right place to
revisit true CAT-replacement for N1MM/HRD, if wanted, once this phase's networking
foundation exists.
Phase 10: Remote audio + APRS beacon (combined) — NAudio on Windows, AVFoundation
wrapper on macOS. Beacon target: APRS-Command (formerly CrossPlatformAPRS) — bridge
mechanism to be determined once APRS-Command's ingestion method is reviewed.
Phase 11: Clickable radio front-panel control — user-photographed or vector image with
transparent clickable regions overlaid, wired to existing CI-V commands. Image source
must be the user's own photo, a licensed image, or an original illustration.
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
- Do not write to HRD Logbook's SQLite database without defensive schema checks
- Do not broadcast N1MM/WSJT-X/HRD UDP traffic to anything other than 127.0.0.1 or an
  explicitly user-configured LAN address
- Do not make any external integration a dependency that could prevent a QSO from being
  recorded in IcomRigControl's own local log
- Do not mark a phase COMPLETE without also updating UserManual.md in the same commit
- Do not let any ICallsignLookupSource implementation throw an exception under any
  condition
- Do not reimplement ARRL's TQSL signing/certificate logic — always shell out to the
  real tqsl executable via ITqslProcessRunner
## Radio Addresses
IC-7300: 0x94 (controller default: 0xE0)
IC-7300MK2: 0xB6 (controller default: 0xE0)
## Key References
- IC-7300MK2 CI-V Reference Guide: icomuk.co.uk/files/icom/PDF/productAdditionalFile/IC-7300MK2_ENG_CI-V_0.pdf
- wfview source: github.com/wf-group/wfview
- Master reference PDF: IcomRigControl_Master_Reference.pdf
- ADIF spec: adif.org
- QRZ.com XML API (Phase 8c): xml.qrz.com, spec at qrz.com/docs/xml/current_spec.html
- HamQTH XML API (Phase 8c): hamqth.com/developers.php
- Callook.info API (Phase 8c): callook.info/api_reference.php
- ARRL LoTW / TQSL (Phase 8d): lotw.arrl.org — upload endpoint lotw.arrl.org/lotwuser/upload,
  query endpoint lotw.arrl.org/lotwuser/lotwreport.adi
- HRD Logbook docs (Phase 8e): support.hamradiodeluxe.com
- N1MM External UDP Messages docs (Phase 8f): n1mmwp.hamdocs.com/appendices/external-udp-broadcasts/
- UserManual.md (project root): end-user documentation
## Related Projects
- APRS-Command (formerly CrossPlatformAPRS, KE4CON): APRS beacon target for Phase 10
- EMMCOM Field Comms Server: Phase 6 — COMPLETE
- Ham Radio Deluxe: user's PRIMARY day-to-day logger, not being replaced — see Phase 8e
- N1MM Logger+: user's preferred tool for rare/frequently-changing contests — Phase 8f COMPLETE
- WSJT-X: shares the same UDP protocol family as N1MM (Phase 8f, COMPLETE)
## Session Start Checklist
1. Read this file
2. Confirm which Phase is active
3. Check that the layer being touched matches the Phase
4. Do not refactor other layers unless the current Phase explicitly requires it
5. When completing a phase this session, update UserManual.md in the same commit
## Deployment Targets
Headless CI-V server (Phase 9, no UI): Raspberry Pi 4 or 5, 2GB minimum, 4GB comfortable.
Full Avalonia UI + scope on Pi: Raspberry Pi 5, 8GB RAM.
Storage: 16-32GB microSD (A2 rated recommended).
## Supported Desktop Platforms (all four, via Avalonia 11)
Windows: primary development platform as of Phase 3 — VS Code + GitHub Desktop
macOS: fully supported secondary/testing platform
Linux desktop: full support
Raspberry Pi OS (Linux ARM64): Pi 5, 8GB
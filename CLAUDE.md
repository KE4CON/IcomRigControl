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
  completed phase adds or changes for the person actually using the app.
Whenever a phase (or sub-phase) is marked COMPLETE in Feature Priorities below,
UserManual.md MUST be updated in the same commit. UserManual.md has no automatic
"read at session start" mechanism the way CLAUDE.md does — this rule is what keeps it
current in place of that automation.
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
  DataGrid 12.1.0 has a confirmed row-rendering bug — reported upstream to
  AvaloniaUI/Avalonia.Controls.DataGrid.
- Environment.SpecialFolder.MyDocuments resolves to the OneDrive-redirected Documents path
  on this machine, not plain C:\Users\jrosp\Documents. Always verify actual file output
  location when debugging file I/O.
- Network-calling services must never throw back to the Transceiver's event dispatch —
  catch and record errors internally (LastError property), never crash the polling loop.
- QsoLogger writes through to a persistent local ADIF file as each QSO is logged.
  COMPLETE — see Core Design Principle above.
- Neither IC-7300 nor IC-7300MK2 has a built-in TNC or APRS engine. APRS must be built as
  software AFSK/AX.25 packet audio played out through an audio device, hence merged with
  the audio phase.
- Custom-drawn UI controls sometimes need an explicit InvalidateVisual() call on the
  specific child element (e.g. the Image), not just the parent UserControl. Confirmed on
  Avalonia 12.1.0 with WriteableBitmap-backed Image controls.
- Never reproduce copyrighted product photography as an asset in this project.
- HRD Logbook's SQLite schema is reverse-engineered from community sources, not
  officially published — any direct-write integration must be best-effort, defensive,
  and never a replacement for the ADIF path.
- N1MM/WSJT-X/HRD UDP integration uses a public, documented XML-over-UDP protocol
  (N1MM's External UDP Messages, port 12060; WSJT-X port 2333; HRD's UDP Receive feature
  consumes the same protocol). One shared broadcaster/listener pair serves all three.
- Do not attempt to make N1MM or HRD's own rig control literally driven by
  IcomRigControl (CAT replacement) as part of Phase 8 — that's Phase 9 territory.
## Feature Priorities (build in this order)
Phase 1: CI-V engine + serial connection + frequency read/set + mode read/set — COMPLETE (23 passing tests)
Phase 2: Meter polling — COMPLETE (43 passing tests)
Phase 3: Avalonia UI — main panel — COMPLETE
Phase 4: Memory bulk editor — COMPLETE (52 passing tests)
Phase 5: Activity logger (CSV) — COMPLETE (56 passing tests)
Phase 6: EMMCOM dashboard integration — COMPLETE (60 passing tests)
Phase 7: Spectrum scope capture and waterfall display — CORE COMPLETE (74 passing tests). REMAINING: frequency axis labels; click-to-tune.
Phase 8: ADIF logging (general + contest + callsign lookup + LoTW + HRD + N1MM/WSJT-X) — ACTIVE.
  8a. Core logging — COMPLETE: QsoRecord, AdifWriter, QsoLogger with persistent
  write-through to a timestamped session file on every LogQso call. 118 passing tests.
  REMAINING: logging UI panel (quick-entry fields, Log QSO button, running table).
  8b. Contest mode — COMPLETE: ContestDefinition, ContestCatalog with ARRL Field Day,
  ContestScoreCalculator. REMAINING: additional contest catalog entries beyond Field Day;
  live running score display in UI.
  8c. Callsign lookup — NOT YET BUILT. ICallsignLookupSource interface, QRZ/HamQTH/Callook
  implementations, user-selectable in Settings.
  8d. LoTW upload/download — NOT YET BUILT. LotwBridge shelling out to TQSL.
  8e. Ham Radio Deluxe integration (three layers) — NOT YET BUILT.
    Layer 1 (status feed) — shares RadioInfoUdpBroadcaster infrastructure from 8f,
    pointed at HRD's UDP Receive feature. Ready to wire up now that the broadcaster
    exists (see 8f Direction 1, COMPLETE) — just needs a destination added and UI exposure.
    Layer 2 (ADIF handoff) — should already work via existing AdifWriter; verify once
    near a machine with HRD installed.
    Layer 3 (HrdSqliteBridge direct write, bonus/best-effort) — NOT YET BUILT.
  8f. N1MM Logger+, WSJT-X, and HRD UDP integration — COMPLETE (both directions).
    Direction 1 (send) — COMPLETE: RadioInfoUdpBroadcaster sends RadioInfo-format XML
    packets (frequency, mode, PTT state) to a configurable list of destination IP:port
    targets whenever Transceiver's FrequencyChanged/ModeChanged/PttChanged events fire.
    Built generically so the same broadcaster instance can feed N1MM, HRD, and/or
    WSJT-X simultaneously by adding multiple destinations. Proven with a real UDP
    send/receive test over an actual loopback socket. 6 passing tests.
    Direction 2 (receive) — COMPLETE: ContactPacketParser + ContactUdpListener receive
    and parse Contact-format XML packets from N1MM/WSJT-X/HRD, mirroring valid contacts
    into QsoLogger via LogReceivedQso. Proven with a real UDP integration test. 13
    passing tests (9 parser + 4 listener).
    137 passing tests total across all of Phase 8f. REMAINING: expose Start/Stop toggles
    and destination/port configuration in the UI for both directions (currently only
    instantiable in code, not user-configurable through the app itself) — this is the
    one piece standing between "built and tested" and "usable by Jim without editing
    code." Update UserManual.md §8 once exposed in UI, per Documentation Requirements.
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
- Do not store QRZ/HamQTH/LoTW credentials in plain text in source-controlled files
- Do not write to HRD Logbook's SQLite database without defensive schema checks
- Do not broadcast N1MM/WSJT-X/HRD UDP traffic to anything other than 127.0.0.1 or an
  explicitly user-configured LAN address
- Do not make any external integration a dependency that could prevent a QSO from being
  recorded in IcomRigControl's own local log
- Do not mark a phase COMPLETE without also updating UserManual.md in the same commit
## Radio Addresses
IC-7300: 0x94 (controller default: 0xE0)
IC-7300MK2: 0xB6 (controller default: 0xE0)
## Key References
- IC-7300MK2 CI-V Reference Guide: icomuk.co.uk/files/icom/PDF/productAdditionalFile/IC-7300MK2_ENG_CI-V_0.pdf
- wfview source: github.com/wf-group/wfview
- Master reference PDF: IcomRigControl_Master_Reference.pdf
- ADIF spec: adif.org
- QRZ.com XML API (Phase 8c): xml.qrz.com
- HamQTH XML API (Phase 8c): hamqth.com/developers.php
- Callook.us API (Phase 8c): callook.info
- ARRL LoTW / TQSL (Phase 8d): lotw.arrl.org
- HRD Logbook docs (Phase 8e): support.hamradiodeluxe.com
- N1MM External UDP Messages docs (Phase 8f): n1mmwp.hamdocs.com/appendices/external-udp-broadcasts/
- UserManual.md (project root): end-user documentation
## Related Projects
- APRS-Command (formerly CrossPlatformAPRS, KE4CON): APRS beacon target for Phase 10
- EMMCOM Field Comms Server: Phase 6 — COMPLETE
- Ham Radio Deluxe: user's PRIMARY day-to-day logger, not being replaced — see Phase 8e
- N1MM Logger+: user's preferred tool for rare/frequently-changing contests — see Phase 8f, COMPLETE
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
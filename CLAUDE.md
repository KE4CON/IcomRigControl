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
callsign lookup (Callook/QRZ/HamQTH), LoTW bridge (via TQSL), HRD SQLite bridge,
RadioInfo UDP broadcaster, N1MM/WSJT-X UDP listener, SettingsService. Consume RigModel only.
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
- HRD Logbook's SQLite schema is reverse-engineered from community sources — HrdSqliteBridge
  is defensive (checks table existence before any write, catches all exceptions, returns
  false rather than throwing) and is purely a bonus layer on top of the always-reliable
  ADIF export path, never a replacement for it. COMPLETE — see Phase 8e.
- N1MM/WSJT-X/HRD UDP integration uses a public, documented XML-over-UDP protocol. One
  shared broadcaster/listener pair serves all three.
- Do not attempt to make N1MM or HRD's own rig control literally driven by
  IcomRigControl (CAT replacement) as part of Phase 8 — that's Phase 9 territory.
- ICallsignLookupSource implementations must NEVER throw. QRZ and HamQTH both use
  session-based login (login once, cache, reuse, re-login only on session error).
- LoTW signing MUST be delegated to ARRL's own TQSL tool via ITqslProcessRunner — never
  reimplement ARRL's certificate/signing logic in-house.
- Microsoft.Data.Sqlite (used by HrdSqliteBridge) transitively pulls a vulnerable native
  SQLite build via SQLitePCLRaw.lib.e_sqlite3 2.1.11 (CVE-2025-6965 / GHSA-2m69-gcr7-jv3q),
  unpatched by Microsoft as of this writing. Every project referencing
  Microsoft.Data.Sqlite (currently Services and Tests) MUST also explicitly pin
  SQLitePCLRaw.lib.e_sqlite3 to 3.50.3 or later to remediate this.
- Never store QRZ/HamQTH credentials or any other secrets in AppSettings' backing JSON
  file within source control — settings.json is in .gitignore; keep it that way.
- LARGE CODE BLOCKS FROM CLAUDE CAN GET COPIED INCOMPLETE IF THE COPY BUTTON IS CLICKED
  BEFORE THE BLOCK HAS FULLY SCROLLED INTO VIEW. Root-caused on this machine: the chat
  UI appears to only have the currently-rendered/scrolled portion of a long code block
  available at copy time — clicking Copy without first scrolling all the way to the
  bottom of the block can silently copy only the rendered portion, producing a
  truncated paste with no error at copy time, paste time, or save time. The truncation
  only surfaces later as an XML/XAML/C# parse error, often far from the actual cut
  point, or (worse) as a file that looks fine but is missing its later content entirely.
  This caused real problems across a full session (MainWindow.axaml repeatedly, and one
  file — TqslProcessRunner.cs — was lost between sessions this way). CONFIRMED FIX:
  scroll all the way to the bottom of a long code block BEFORE clicking Copy. A plain
  select-all/delete/paste into an already-open tab works completely reliably once this
  is done — the earlier workaround of File -> New File -> Save As was masking the
  symptom, not fixing the actual cause, and is no longer necessary. Still good practice
  after any large-file rewrite: verify actual size with `dir <file>` (Windows) and, for
  anything safety-critical, spot check that the last line of the intended content is
  actually present (e.g. `type <file> | findstr "<last line text>"`) before trusting a
  large paste completed correctly.
## Feature Priorities (build in this order)
Phase 1: CI-V engine + serial connection + frequency read/set + mode read/set — COMPLETE (23 passing tests)
Phase 2: Meter polling — COMPLETE (43 passing tests)
Phase 3: Avalonia UI — main panel — COMPLETE
Phase 4: Memory bulk editor — COMPLETE (52 passing tests)
Phase 5: Activity logger (CSV) — COMPLETE (56 passing tests)
Phase 6: EMMCOM dashboard integration — COMPLETE (60 passing tests)
Phase 7: Spectrum scope capture and waterfall display — CORE COMPLETE (74 passing tests). REMAINING: frequency axis labels; click-to-tune.
Phase 8: ADIF logging (general + contest + callsign lookup + LoTW + HRD + N1MM/WSJT-X) — ENGINE + SETTINGS WIRING COMPLETE.
  All six sub-phases (8a-8f) are functionally complete at the engine/service level (173
  passing tests project-wide). SettingsWindow exists and is reachable from the main
  dashboard's Settings button. MainWindowViewModel now reads AppSettings at startup
  (ApplySettings method) and instantiates the real services accordingly:
  ICallsignLookupSource (Callook/QRZ/HamQTH per saved selection), LotwBridge (if a TQSL
  path is configured), HrdSqliteBridge (if enabled with a database path),
  RadioInfoUdpBroadcaster and ContactUdpListener (if their respective toggles are on).
  Settings are re-applied automatically when the Settings window closes (stops and
  restarts the UDP broadcaster/listener with the new values), so changes take effect
  without an app restart. An IntegrationsStatus display property summarizes what's
  active. Confirmed building and running cleanly with default (empty) settings.
  8a. Core logging — COMPLETE (118 tests). REMAINING: logging UI panel.
  8b. Contest mode — COMPLETE (Field Day). REMAINING: additional contests; live score UI.
  8c. Callsign lookup — COMPLETE, all three sources (20 tests), now wired into runtime.
  REMAINING: wiring into the logging UI panel once it exists.
  8d. LoTW upload/download — COMPLETE (6 tests), now wired into runtime. REMAINING:
  upload/download buttons in the UI; matching downloaded confirmations against local
  QsoRecords.
  8e. Ham Radio Deluxe integration — COMPLETE (Layer 3: 6 tests), now wired into runtime.
  8f. N1MM Logger+, WSJT-X, and HRD UDP integration — COMPLETE, both directions (137
  tests), now wired into runtime and confirmed re-applying correctly on Settings close.
  REMAINING FOR PHASE 8 OVERALL: display IntegrationsStatus in the main dashboard's UI
  (currently exists as a ViewModel property but has no corresponding XAML binding yet);
  the logging UI panel (8a); contest score UI (8b); LoTW upload/download buttons (8d).
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
- Do not let any ICallsignLookupSource implementation throw an exception under any
  condition
- Do not reimplement ARRL's TQSL signing/certificate logic
- Do not remove the SQLitePCLRaw.lib.e_sqlite3 version pin from any project referencing
  Microsoft.Data.Sqlite without confirming the underlying CVE is resolved upstream first
- Do not trust a large paste into an already-open editor tab or terminal command without
  verifying actual file size afterward — see the large-paste-truncation note above
- Do not assume a prior session's file set is intact — run `dotnet build` at the start
  of any session to catch files silently lost to paste truncation before building on them
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
- ARRL LoTW / TQSL (Phase 8d): lotw.arrl.org
- HRD Logbook docs (Phase 8e): support.hamradiodeluxe.com
- N1MM External UDP Messages docs (Phase 8f): n1mmwp.hamdocs.com/appendices/external-udp-broadcasts/
- SQLitePCLRaw CVE tracking: github.com/dotnet/efcore/issues/38257,
  github.com/advisories/GHSA-2m69-gcr7-jv3q
- UserManual.md (project root): end-user documentation
## Related Projects
- APRS-Command (formerly CrossPlatformAPRS, KE4CON): APRS beacon target for Phase 10
- EMMCOM Field Comms Server: Phase 6 — COMPLETE
- Ham Radio Deluxe: user's PRIMARY day-to-day logger, not being replaced — Phase 8e COMPLETE
- N1MM Logger+: user's preferred tool for rare/frequently-changing contests — Phase 8f COMPLETE
- WSJT-X: shares the same UDP protocol family as N1MM (Phase 8f, COMPLETE)
## Session Start Checklist
1. Read this file
2. Run `dotnet build` before making any changes to confirm the prior session's file set
   is genuinely intact — see the large-paste-truncation note above
3. Confirm which Phase is active
4. Check that the layer being touched matches the Phase
5. Do not refactor other layers unless the current Phase explicitly requires it
6. When completing a phase this session, update UserManual.md in the same commit
7. For any file edit exceeding roughly 100 lines, use the File -> New File -> Save As
   approach rather than pasting into an existing tab — see the large-paste-truncation
   note in Coding Standards above
## Deployment Targets
Headless CI-V server (Phase 9, no UI): Raspberry Pi 4 or 5, 2GB minimum, 4GB comfortable.
Full Avalonia UI + scope on Pi: Raspberry Pi 5, 8GB RAM.
Storage: 16-32GB microSD (A2 rated recommended).
## Supported Desktop Platforms (all four, via Avalonia 11)
Windows: primary development platform as of Phase 3 — VS Code + GitHub Desktop
macOS: fully supported secondary/testing platform
Linux desktop: full support
Raspberry Pi OS (Linux ARM64): Pi 5, 8GB
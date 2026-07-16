# IcomRigControl — Project Rules (CLAUDE.md)
## Project Identity
Name: IcomRigControl
Author: Jim, KE4CON
Language: C# (.NET 10)
UI Framework: Avalonia 12.x (cross-platform desktop — macOS, Windows, Linux, Raspberry Pi).
See "Avalonia Version Decision" below before considering any version change.
Target Radios: Icom IC-7300 (address 94h) and IC-7300MK2 (address B6h)
Connection: USB serial via System.IO.Ports (115200 baud default); TCP/network mode
COMPLETE — see Phase 9.
## Core Design Principle: Resilience / Backup-of-Record (EMCOMM discipline)
IcomRigControl's own QsoLogger is the resilient backup of record for all logged QSOs,
independent of whether HRD Logbook, N1MM, or any other external program is running,
reachable, or healthy. Every QSO logged anywhere in this ecosystem must land in
IcomRigControl's own persistent local log. Integrations with HRD, N1MM, LoTW, etc. are
one-way, best-effort *additions* on top of this local log — never a dependency the local
log needs to function, and never a gate that can cause a QSO to go unrecorded if the
external program is down. This mirrors the user's EMCOMM "always have a backup plan"
principle, applied to logging infrastructure.
## Avalonia Version Decision (researched and decided — do not revisit without new evidence)
This project stays on Avalonia 12.x. Researched and decided after the third confirmed UI
rendering bug (CheckBox, following the DataGrid and WaterfallControl Image repaint bugs).
Findings: the "control invisible until interaction" symptom is a real, filed upstream
Avalonia issue (AvaloniaUI/Avalonia#20726), explicitly reported as new to the 12.x line.
DECISION: stay on 12.x. Working, tested fixes exist for all three known rendering bugs.
Revisit only if a new, genuinely unexplainable rendering bug appears.
## Architecture Layers (never mix concerns across layers)
Layer 1 — CivEngine: Raw CI-V framing, serial port I/O, BCD encode/decode. No UI, no
radio model.
Layer 2 — RigModel: Transceiver class exposing clean C# properties and events. Consumes
CivEngine only. Also hosts the Phase 9 network layer (CivNetworkProtocol, CivTcpServer,
TcpCivTransport) since these implement/wrap ICivTransport, which is defined here.
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
- AVOID Avalonia's CheckBox control in this project — CONFIRMED BUG (see Avalonia Version
  Decision above). Use a ToggleButton bound to the same boolean property instead.
- FOR EVENLY-SPACED ITEMS ACROSS A CONTAINER'S WIDTH: use ItemsControl with a UniformGrid
  (Rows="1") as the ItemsPanel, NOT Canvas positioning with a value converter computing
  pixel offsets from a fraction — confirmed unreliable in Phase 7.
- FOR PER-ROW LOOKUPS AGAINST A VIEWMODEL-LEVEL COLLECTION (e.g. "is this row's callsign
  in the ViewModel's ConfirmedCallsigns list?"): a single-value IValueConverter cannot see
  the ViewModel's collection — use IMultiValueConverter with a MultiBinding instead,
  passing the row's own property plus a binding to the ViewModel collection via
  $parent[ItemsControl].((vm:YourViewModel)DataContext).YourCollection. See
  LotwConfirmedConverter for the working pattern (Phase 8d).
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
- ICallsignLookupSource implementations must NEVER throw. QRZ and HamQTH both use
  session-based login (login once, cache, reuse, re-login only on session error).
- LoTW signing MUST be delegated to ARRL's own TQSL tool via ITqslProcessRunner — never
  reimplement ARRL's certificate/signing logic in-house. Download-and-match (checking
  which local QSOs have LoTW confirmations) matches on callsign + band + date, and stores
  matches in QsoLoggerViewModel.ConfirmedCallsigns rather than mutating QsoRecord (which
  is an immutable record) — the UI checks membership in this collection per-row via
  LotwConfirmedConverter. COMPLETE — see Phase 8d.
- Microsoft.Data.Sqlite (used by HrdSqliteBridge) transitively pulls a vulnerable native
  SQLite build via SQLitePCLRaw.lib.e_sqlite3 2.1.11 (CVE-2025-6965 / GHSA-2m69-gcr7-jv3q),
  unpatched by Microsoft as of this writing. Every project referencing
  Microsoft.Data.Sqlite (currently Services and Tests) MUST also explicitly pin
  SQLitePCLRaw.lib.e_sqlite3 to 3.50.3 or later to remediate this.
- Never store QRZ/HamQTH credentials or any other secrets in AppSettings' backing JSON
  file within source control — settings.json is in .gitignore; keep it that way. The same
  applies to Phase 9's RemoteAuthToken.
- LARGE CODE BLOCKS FROM CLAUDE CAN GET COPIED INCOMPLETE IF THE COPY BUTTON IS CLICKED
  BEFORE THE BLOCK HAS FULLY SCROLLED INTO VIEW. CONFIRMED FIX: scroll all the way to the
  bottom of a long code block BEFORE clicking Copy. If a full-file replacement still
  doesn't seem to land correctly on the FIRST attempt (verified by checking with findstr
  after "saving"), do not keep re-pasting into the same tab — switch immediately to
  File -> New File -> paste -> Save As -> Overwrite. Confirmed in Phase 8d: a
  same-tab paste can silently fail to take effect even when the user believes it saved.
- WHEN PASTING A CODE SNIPPET, VERIFY IT LANDED IN THE INTENDED FILE, NOT AN ADJACENT
  OPEN TAB. Check with `findstr` if a build error shows syntax from the wrong file type.
- Phase 9's remote CI-V protocol requires a non-empty auth token before a CivTcpServer
  will relay any traffic — ValidateToken() rejects an empty expected token by design.
- Avalonia UI projects built with OutputType=WinExe will silently swallow
  Console.WriteLine output — fix via AttachConsole(ATTACH_PARENT_PROCESS) P/Invoke on
  Windows specifically, for any code path meant to run as a CLI tool.
## UI Design (flagged for future work, not yet scheduled as a phase)
User has indicated the current UI ("functional-first, each feature bolted on as its own
bordered box in a single scrolling window") is not satisfying and wants a real design
pass at some point. Not urgent, but should be picked up deliberately rather than by
continuing to bolt on more boxes. When that work starts, clarify with the user first:
color palette/theme direction (stay dark, or open to alternatives), whether to lean into
a real-transceiver-panel aesthetic (ties into Phase 11's clickable radio image) versus a
modern flat dashboard look, and whether to move off one long scrolling window toward tabs
or a more deliberate panel layout now that there are many features. Do not start a redesign
without this input.
## Feature Priorities (build in this order)
Phase 1: CI-V engine + serial connection + frequency read/set + mode read/set — COMPLETE (23 passing tests)
Phase 2: Meter polling — COMPLETE (43 passing tests)
Phase 3: Avalonia UI — main panel — COMPLETE
Phase 4: Memory bulk editor — COMPLETE (52 passing tests)
Phase 5: Activity logger (CSV) — COMPLETE (56 passing tests)
Phase 6: EMMCOM dashboard integration — COMPLETE (60 passing tests)
Phase 7: Spectrum scope capture and waterfall display — COMPLETE (180 passing tests).
Phase 8: ADIF logging (general + contest + callsign lookup + LoTW + HRD + N1MM/WSJT-X) — COMPLETE, ZERO REMAINING ITEMS.
  All six sub-phases (8a-8f) fully complete at both the engine/service level AND
  reachable through working UI, including the final piece (8d's LoTW upload/download
  buttons, wired to LotwBridge, with a per-row confirmed indicator using
  LotwConfirmedConverter's IMultiValueConverter pattern). Confirmed working live
  end-to-end, including correct "not configured" messaging when TQSL isn't set up.
  198 passing tests project-wide. No remaining items in Phase 8 at all.
Phase 9: Remote/network mode — COMPLETE (198 passing tests, included in the count above).
  Full client-server remote CI-V control pipeline designed for LAN, VPN, or
  44Net/AMPRNet. REMAINING: real-world testing against actual IC-7300 hardware over an
  actual network link — everything proven via localhost sockets and the demo transport
  to date, genuinely working but not yet exercised against real hardware + a real
  network path.
Phase 10: Remote audio + APRS beacon (combined) — NAudio on Windows, AVFoundation
wrapper on macOS. Beacon target: APRS-Command (formerly CrossPlatformAPRS) — bridge
mechanism to be determined once APRS-Command's ingestion method is reviewed.
Phase 11: Clickable radio front-panel control — user-photographed or vector image with
transparent clickable regions overlaid, wired to existing CI-V commands. Image source
must be the user's own photo, a licensed image, or an original illustration. See UI
Design note above — may be a natural companion to a broader UI redesign pass.
## What NOT to do
- Do not implement features out of phase order without explicit instruction
- Do not add NuGet packages without listing them here first
- Do not put CI-V logic in ViewModels
- Do not put UI code in CivEngine or RigModel
- Do not use Thread.Sleep — use Task.Delay with CancellationToken
- Do not swallow exceptions silently — log and surface them
- Do not hardcode radio addresses — read from config
- Do not store QRZ/HamQTH/LoTW/Remote auth credentials in plain text in source-controlled files
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
- Do not trust a large paste without verifying it scrolled fully into view before the
  copy click, and that it landed in the intended file — if a same-tab paste doesn't
  verifiably take effect on the first try, switch to File -> New File -> Save As
- Do not assume a prior session's file set is intact — run `dotnet build` at the start
  of any session to catch files silently lost to paste truncation before building on them
- Do not use Avalonia's CheckBox control in this project — use ToggleButton instead
- Do not use Canvas + IValueConverter for evenly-spaced item layout — use
  ItemsControl + UniformGrid instead
- Do not use a single-value IValueConverter to check a row against a ViewModel-level
  collection — use IMultiValueConverter + MultiBinding instead
- Do not begin a UI redesign pass without first getting the user's input on theme/layout
  direction — see UI Design note above
- Do not downgrade Avalonia to 11.x without new evidence — see Avalonia Version Decision
  above; this has already been researched and decided
- Do not run a CivTcpServer with a blank/empty auth token
- Do not assume Console.WriteLine output is visible in a WinExe-built app without
  AttachConsole
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
- Avalonia 12 rendering bug tracking: github.com/AvaloniaUI/Avalonia/issues/20726
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
   is genuinely intact
3. Confirm which Phase is active
4. Check that the layer being touched matches the Phase
5. Do not refactor other layers unless the current Phase explicitly requires it
6. When completing a phase this session, update UserManual.md in the same commit
7. Before any large paste: scroll to the bottom of the code block first. After any
   paste into an existing file: verify it landed correctly (findstr/dir), and if a
   same-tab paste doesn't verifiably take on the first try, switch to
   File -> New File -> Save As rather than retrying the same tab
8. Never use CheckBox in new UI work — use ToggleButton
9. For evenly-spaced item layout, use ItemsControl + UniformGrid, not Canvas + converter
10. For per-row lookups against a ViewModel collection, use IMultiValueConverter +
    MultiBinding, not a single-value converter
## Deployment Targets
Headless CI-V server (Phase 9): Raspberry Pi 4 or 5, 2GB minimum, 4GB comfortable —
runnable via `IcomRigControl.UI --headless-server`.
Full Avalonia UI + scope on Pi: Raspberry Pi 5, 8GB RAM.
Storage: 16-32GB microSD (A2 rated recommended).
## Supported Desktop Platforms (all four, via Avalonia 12)
Windows: primary development platform as of Phase 3 — VS Code + GitHub Desktop
macOS: fully supported secondary/testing platform
Linux desktop: full support
Raspberry Pi OS (Linux ARM64): Pi 5, 8GB
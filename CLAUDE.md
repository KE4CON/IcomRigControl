# IcomRigControl — Project Rules (CLAUDE.md)
## Project Identity
Name: IcomRigControl
Author: Jim, KE4CON
Language: C# (.NET 10)
UI Framework: Avalonia 12.x (cross-platform desktop — macOS, Windows, Linux, Raspberry Pi).
See "Avalonia Version Decision" below before considering any version change.
Target Radios: Icom IC-7300 (address 94h) and IC-7300MK2 (address B6h)
Connection: USB serial (local) or TCP/network mode (remote, Phase 9) — COMPLETE.
## Core Design Principle: Resilience / Backup-of-Record (EMCOMM discipline)
IcomRigControl's own QsoLogger is the resilient backup of record for all logged QSOs,
independent of whether HRD Logbook, N1MM, or any other external program is running,
reachable, or healthy. Every QSO logged anywhere in this ecosystem must land in
IcomRigControl's own persistent local log. Integrations with HRD, N1MM, LoTW, etc. are
one-way, best-effort *additions* on top of this local log — never a dependency the local
log needs to function, and never a gate that can cause a QSO to go unrecorded if the
external program is down. This mirrors the user's EMCOMM "always have a backup plan"
principle, applied to logging infrastructure.
## Comprehensive User Manual (planned, deliberately deferred — do not start early)
User has explicitly requested a professional, incredibly complete, step-by-step manual
with nothing left out (real screenshots, verified click-by-click steps, every known
quirk documented as troubleshooting). TRIGGER CONDITION to begin: macOS audio is now
DONE (see Phase 10 below) — the remaining prerequisite is an explicit decision on the
UI redesign question (see "UI Design" note below): either it happens, or the user
consciously decides to keep the current layout and move on. Once triggered: dedicate
session(s) specifically to this — walk through every window in the running app together,
capture real screenshots, verify every instruction against live behavior, and fold in
every known quirk from Coding Standards/What NOT to do as troubleshooting entries. The
existing UserManual.md remains the current, functional (if less exhaustive) reference in
the meantime.
## Cross-Platform Lesson: Platform-Specific Services Must Be Selected at EVERY
## Construction Site, Not Just One (learned the hard way, Phase 10 macOS session)
When a service has multiple platform-specific implementations behind a shared interface
(e.g. IAudioPlayer: NAudioPlayer for Windows, MacAudioPlayer for macOS), EVERY place in
the codebase that constructs that service must use the platform-aware selection
(`OperatingSystem.IsWindows() ? new NAudioPlayer() : new MacAudioPlayer()`), not just the
first place it was introduced. CONFIRMED REAL BUG: MainWindowViewModel was correctly
fixed to do platform-aware selection, but SettingsViewModel — a separate ViewModel that
also needs an IAudioPlayer to enumerate audio devices for its dropdown — still had a
hardcoded `new NAudioPlayer()`, which crashed the entire app with an unhandled
PlatformNotSupportedException the moment a macOS user clicked Settings. This was NOT
caught by the test suite, because NAudioPlayerTests correctly skips on non-Windows
platforms — the crash only showed up when actually running the live app on macOS.
LESSON: when introducing a new platform-specific interface implementation, grep the
whole codebase for the OLD concrete type's name (e.g. `new NAudioPlayer()`) to find
every construction site, not just the one you were actively working on, and don't
consider the migration done until you've run the actual app on the non-default platform
and clicked through every window that might touch it.
## Avalonia Version Decision (researched and decided — do not revisit without new evidence)
This project stays on Avalonia 12.x. Researched and decided after the third confirmed UI
rendering bug (CheckBox, following the DataGrid and WaterfallControl Image repaint bugs).
Findings: the "control invisible until interaction" symptom is a real, filed upstream
Avalonia issue (AvaloniaUI/Avalonia#20726), explicitly reported as new to the 12.x line.
DECISION: stay on 12.x. Working, tested fixes exist for all known rendering bugs.
Revisit only if a new, genuinely unexplainable rendering bug appears.
## Architecture Layers (never mix concerns across layers)
Layer 1 — CivEngine: Raw CI-V framing, serial port I/O, BCD encode/decode, plus Phase 10's
AX.25/APRS/AFSK protocol layer (Ax25FrameBuilder, AprsPositionFormatter, AfskModulator,
AfskProfile, WavFileWriter). No UI, no radio model.
Layer 2 — RigModel: Transceiver class exposing clean C# properties and events. Consumes
CivEngine only. Also hosts the Phase 9 network layer (CivNetworkProtocol, CivTcpServer,
TcpCivTransport) since these implement/wrap ICivTransport, which is defined here.
Layer 3 — Services: Logger, EMMCOM bridge, ADIF logger, callsign lookup (Callook/QRZ/
HamQTH), LoTW bridge (via TQSL), HRD SQLite bridge, RadioInfo UDP broadcaster,
N1MM/WSJT-X UDP listener, SettingsService, IAudioPlayer with two platform implementations
(NAudioPlayer for Windows via WASAPI, MacAudioPlayer for macOS via afplay — see the
Cross-Platform Lesson above), AprsBeaconService, PeriodicBeaconScheduler. Consume RigModel only.
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
- For DataGrid-style tabular UI: prefer ItemsControl + DataTemplate over Avalonia.Controls.DataGrid.
- AVOID Avalonia's CheckBox control — CONFIRMED BUG. Use a ToggleButton instead.
- FOR EVENLY-SPACED ITEMS ACROSS A CONTAINER'S WIDTH: use ItemsControl + UniformGrid
  (Rows="1"), NOT Canvas + IValueConverter — confirmed unreliable in Phase 7.
- FOR PER-ROW LOOKUPS AGAINST A VIEWMODEL-LEVEL COLLECTION: use IMultiValueConverter with
  a MultiBinding, not a single-value IValueConverter.
- FOR TOGGLEBUTTON BOUND TO BOTH IsChecked AND Command: set IsChecked's binding Mode to
  OneWay when the Command's handler itself sets the underlying property.
- EACH NEW ToggleButton NEEDS ITS OWN DEDICATED TEXT CONVERTER, not a reused one.
- EVERY ToggleButton NEEDS THE FULL THREE-SELECTOR STYLE SET (:checked, :pointerover,
  :checked:pointerover), copied completely each time.
- WHEN A SERVICE HAS MULTIPLE PLATFORM-SPECIFIC IMPLEMENTATIONS, grep for every
  construction site of the old/single implementation before considering a platform port
  done — see the Cross-Platform Lesson section above.
- Environment.SpecialFolder.MyDocuments resolves to a cloud-sync-redirected Documents
  path (OneDrive on this Windows machine, potentially similar with iCloud Drive on macOS
  if the project itself or user folders are under iCloud sync) — always verify actual
  file output location when debugging file I/O, on either platform.
- Network-calling services must never throw back to the Transceiver's event dispatch —
  catch and record errors internally, never crash the polling loop.
- QsoLogger writes through to a persistent local ADIF file as each QSO is logged.
  COMPLETE — see Core Design Principle above.
- Neither IC-7300 nor IC-7300MK2 has a built-in TNC or APRS engine. APRS is built as
  software AFSK/AX.25 packet audio played out through an audio device. COMPLETE on BOTH
  Windows (NAudio/WASAPI) and macOS (afplay via Process.Start, same "shell out to the
  OS's own tool" pattern as TqslProcessRunner rather than binding to AVFoundation
  directly — afplay has no device-selection capability of its own, so macOS playback
  always uses the system's current default output device) — see Phase 10. Real-world HF
  APRS tones (1600/1800 Hz, 300 baud) are used, NOT the historical literal Bell 103
  telephone-modem frequencies.
- Custom-drawn UI controls sometimes need an explicit InvalidateVisual() call on the
  specific child element (e.g. the Image), not just the parent UserControl.
- Never reproduce copyrighted product photography as an asset in this project.
- HRD Logbook's SQLite schema is reverse-engineered from community sources —
  HrdSqliteBridge is defensive and purely a bonus layer. COMPLETE — see Phase 8e.
- N1MM/WSJT-X/HRD UDP integration COMPLETE — see Phase 8f.
- ICallsignLookupSource implementations must NEVER throw. COMPLETE — see Phase 8c.
- LoTW signing MUST be delegated to ARRL's own TQSL tool via ITqslProcessRunner.
  COMPLETE — see Phase 8d.
- Microsoft.Data.Sqlite transitively pulls a vulnerable native SQLite build via
  SQLitePCLRaw.lib.e_sqlite3 2.1.11 (CVE-2025-6965). Every project referencing
  Microsoft.Data.Sqlite MUST also explicitly pin SQLitePCLRaw.lib.e_sqlite3 to 3.50.3+.
- Never store QRZ/HamQTH credentials, Phase 9's RemoteAuthToken, or any other secrets in
  AppSettings' backing JSON file within source control — settings.json is in .gitignore.
- LARGE CODE BLOCKS FROM CLAUDE CAN GET COPIED INCOMPLETE IF THE COPY BUTTON IS CLICKED
  BEFORE THE BLOCK HAS FULLY SCROLLED INTO VIEW. CONFIRMED FIX: scroll to the bottom
  before clicking Copy.
- ON WINDOWS: if a full-file replacement doesn't verifiably land on the first attempt
  (checked via findstr/dir), switch to File -> New File -> paste -> Save As -> Overwrite
  rather than re-pasting into the same tab.
- ON MACOS: VS Code's Select All (Cmd+A) + Delete has been observed to NOT reliably clear
  editor content on this machine (confirmed by the user directly). DO NOT rely on manual
  VS Code editing for file changes on macOS. Instead, use terminal-based file writes:
  `cat > path/to/file.cs << 'EOF' ... EOF` (heredoc) for full-file writes/creates, or
  `sed -i '' 's/old/new/'` for small, precise single-line or pattern substitutions
  (note the empty '' after -i is required on macOS's BSD sed, unlike Linux/GNU sed which
  takes no argument there). Both confirmed reliable on this machine in the Phase 10
  macOS session. Always verify with `cat`/`grep` after any terminal-based write, then
  `dotnet build` before considering the change complete.
- WHEN PASTING A CODE SNIPPET, VERIFY IT LANDED IN THE INTENDED FILE. Check with
  `findstr` (Windows) or `grep` (macOS/Linux) if a build error shows syntax from the
  wrong file type.
- WHEN MANUALLY EDITING A METHOD (not a full-file replace), re-view the resulting file
  before building if the edit is non-trivial.
- Phase 9's remote CI-V protocol requires a non-empty auth token.
- Avalonia UI projects built with OutputType=WinExe (Windows) will silently swallow
  Console.WriteLine output — fix via AttachConsole(ATTACH_PARENT_PROCESS) P/Invoke.
## UI Design (flagged for future work, not yet scheduled as a phase)
User has indicated the current UI is not satisfying and wants a real design pass. This
decision is now the ONLY remaining prerequisite for starting the Comprehensive User
Manual project above. When redesign work starts, clarify with the user first: color
palette/theme direction, transceiver-panel aesthetic vs. modern flat dashboard, tabs vs.
one long scrolling window.
## Feature Priorities (build in this order)
Phase 1: CI-V engine + serial connection + frequency read/set + mode read/set — COMPLETE (23 tests)
Phase 2: Meter polling — COMPLETE (43 tests)
Phase 3: Avalonia UI — main panel — COMPLETE
Phase 4: Memory bulk editor — COMPLETE (52 tests)
Phase 5: Activity logger (CSV) — COMPLETE (56 tests)
Phase 6: EMMCOM dashboard integration — COMPLETE (60 tests)
Phase 7: Spectrum scope capture and waterfall display — COMPLETE (180 tests).
Phase 8: ADIF logging (general + contest + callsign lookup + LoTW + HRD + N1MM/WSJT-X) —
  COMPLETE, ZERO REMAINING ITEMS. Includes ARRL Field Day and ARRL RTTY Roundup contest
  definitions, both verified against official ARRL rules, selectable in the QSO Logger UI.
Phase 9: Remote/network mode — COMPLETE. REMAINING: real-world testing against actual
  IC-7300 hardware over a real network link.
Phase 10: APRS beacon over HF — FULLY COMPLETE ON BOTH WINDOWS AND MACOS. Ax25FrameBuilder
  (spec-verified), AprsPositionFormatter (spec-verified, matches the official worked
  example exactly), AfskModulator/AfskProfile (correct real-world HF tones), WavFileWriter
  (confirmed by listening — sounds correct), IAudioPlayer with NAudioPlayer (Windows,
  WASAPI, confirmed live) AND MacAudioPlayer (macOS, afplay via Process.Start, confirmed
  live — a real APRS beacon was generated, PTT-keyed, and played audibly through real Mac
  hardware during this session), AprsBeaconService (PTT-keying with guaranteed release),
  PeriodicBeaconScheduler (resilient to failures), full Settings UI, manual Send Beacon
  and automatic Auto Beacon toggle — ALL confirmed working live on BOTH platforms.
  A real cross-platform bug was found and fixed during the macOS session: SettingsViewModel
  had a hardcoded Windows-only NAudioPlayer causing an unconditional crash on macOS when
  opening Settings — see the Cross-Platform Lesson section above for the general
  principle this taught. 259 passing tests (macOS confirmed; Windows count matches as of
  the last Windows session, pending final Windows re-verification of the newest commits).
  NOTHING REMAINING IN PHASE 10.
Phase 11: Clickable radio front-panel control — user-photographed or vector image with
transparent clickable regions overlaid, wired to existing CI-V commands. See UI Design
note above — may be a natural companion to a broader UI redesign pass.
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
- Do not let any ICallsignLookupSource implementation throw an exception under any condition
- Do not reimplement ARRL's TQSL signing/certificate logic
- Do not remove the SQLitePCLRaw.lib.e_sqlite3 version pin without confirming the CVE is
  resolved upstream first
- Do not trust a large paste without verifying it landed correctly
- Do not rely on manual VS Code editing for file changes on macOS — use terminal
  heredoc/sed instead, see Coding Standards above
- Do not assume a prior session's file set is intact — run `dotnet build` at the start
  of any session
- Do not use Avalonia's CheckBox control — use ToggleButton instead
- Do not use Canvas + IValueConverter for evenly-spaced item layout
- Do not use a single-value IValueConverter to check a row against a ViewModel-level
  collection — use IMultiValueConverter + MultiBinding instead
- Do not bind a ToggleButton's IsChecked TwoWay when its Command also drives the same property
- Do not reuse another feature's button-text converter for a new toggle
- Do not write a ToggleButton's style block with only some of the three required selectors
- Do not introduce a new platform-specific service implementation without grepping the
  whole codebase for every construction site of the old one — see Cross-Platform Lesson
- Do not begin a UI redesign pass without first getting the user's input
- Do not begin the Comprehensive User Manual project until the UI redesign question is resolved
- Do not downgrade Avalonia to 11.x without new evidence
- Do not run a CivTcpServer with a blank/empty auth token
- Do not assume Console.WriteLine output is visible in a WinExe-built app without AttachConsole
## Radio Addresses
IC-7300: 0x94 (controller default: 0xE0)
IC-7300MK2: 0xB6 (controller default: 0xE0)
## Key References
- IC-7300MK2 CI-V Reference Guide: icomuk.co.uk/files/icom/PDF/productAdditionalFile/IC-7300MK2_ENG_CI-V_0.pdf
- wfview source: github.com/wf-group/wfview
- ADIF spec: adif.org
- QRZ.com XML API: xml.qrz.com, spec at qrz.com/docs/xml/current_spec.html
- HamQTH XML API: hamqth.com/developers.php
- Callook.info API: callook.info/api_reference.php
- ARRL LoTW / TQSL: lotw.arrl.org
- HRD Logbook docs: support.hamradiodeluxe.com
- N1MM External UDP Messages docs: n1mmwp.hamdocs.com/appendices/external-udp-broadcasts/
- SQLitePCLRaw CVE tracking: github.com/dotnet/efcore/issues/38257,
  github.com/advisories/GHSA-2m69-gcr7-jv3q
- Avalonia 12 rendering bug tracking: github.com/AvaloniaUI/Avalonia/issues/20726
- APRS 1.0.1 spec (position report format): worked example !4903.50N/07201.75W- matched
  exactly in AprsPositionFormatterTests
- ARRL RTTY Roundup rules: contests.arrl.org/ContestRules/RTTY-RU-Rules.pdf
- macOS afplay reference: ss64.com/mac/afplay.html
- UserManual.md (project root): end-user documentation
## Related Projects
- APRS-Command (formerly CrossPlatformAPRS, KE4CON): originally the intended Phase 10
  beacon target; Phase 10 was ultimately built as a self-contained pipeline within
  IcomRigControl rather than bridging to APRS-Command.
- EMMCOM Field Comms Server: Phase 6 — COMPLETE
- Ham Radio Deluxe: user's PRIMARY day-to-day logger — Phase 8e COMPLETE
- N1MM Logger+: Phase 8f COMPLETE
- WSJT-X: Phase 8f COMPLETE
## Session Start Checklist
1. Read this file
2. Run `dotnet build` before making any changes to confirm the prior session's file set
   is genuinely intact
3. Confirm which Phase is active
4. Check that the layer being touched matches the Phase
5. Do not refactor other layers unless the current Phase explicitly requires it
6. When completing a phase this session, update UserManual.md in the same commit
7. On Windows: scroll to bottom before copying large blocks; switch to File -> New File
   -> Save As if a same-tab paste doesn't verifiably take. On macOS: use terminal
   heredoc/sed for file writes, not manual VS Code editing — see Coding Standards.
8. Never use CheckBox in new UI work — use ToggleButton
9. For evenly-spaced item layout, use ItemsControl + UniformGrid, not Canvas + converter
10. For per-row lookups against a ViewModel collection, use IMultiValueConverter + MultiBinding
11. For any new ToggleButton: dedicated text converter, IsChecked Mode=OneWay if a
    Command also drives the property, and the complete three-selector style set
12. Do not start the Comprehensive User Manual project until the UI redesign question is resolved
13. When adding or modifying a platform-specific service, grep the whole codebase for
    every construction site of the type being replaced — see Cross-Platform Lesson above
## Deployment Targets
Headless CI-V server (Phase 9): Raspberry Pi 4 or 5, 2GB minimum, 4GB comfortable —
runnable via `IcomRigControl.UI --headless-server`.
Full Avalonia UI + scope on Pi: Raspberry Pi 5, 8GB RAM.
Storage: 16-32GB microSD (A2 rated recommended).
## Supported Desktop Platforms (all four, via Avalonia 12)
Windows: primary development platform — VS Code + GitHub Desktop
macOS: fully supported secondary platform — Phase 10 audio COMPLETE here too, confirmed
live (real beacon generated and played audibly on real Mac hardware). VS Code manual
editing (Select All + Delete) has been observed unreliable on this machine — use terminal
heredoc/sed for file changes instead, see Coding Standards.
Linux desktop: full support
Raspberry Pi OS (Linux ARM64): Pi 5, 8GB

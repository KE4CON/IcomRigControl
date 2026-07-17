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
quirk documented as troubleshooting). Deliberately NOT starting this yet, and should not
be started prematurely, for two reasons: (1) the UI redesign (see "UI Design" note below)
is still an open, undecided item — writing exhaustive click-by-click steps and capturing
screenshots against a UI that may change soon would need to be redone; (2) proper
completion requires actually walking through the running app together, screen by screen,
capturing real screenshots and verifying every step against the live app — this is
realistically its own multi-session project, not an incremental addition to a feature-
building session. TRIGGER CONDITION to begin: once macOS audio (the one remaining Phase
10 item) is complete AND an explicit decision has been made on the UI redesign (either
it happens, or the user consciously decides to keep the current layout and move on).
Once triggered: dedicate session(s) specifically to this — walk through every window in
the running app together, capture real screenshots, verify every instruction against
live behavior, and fold in every known quirk from the Coding Standards/What NOT to do
sections below as troubleshooting entries. Do not attempt this from memory/code alone
without the live walkthrough — several real quirks (e.g. the ToggleButton hover bug,
the OneDrive path redirect, Settings auto-close behavior) were only discovered through
actually running and clicking the app, and a manual written without that step would
likely omit exactly the details the user is asking for. The existing UserManual.md
remains the current, functional (if less exhaustive) reference in the meantime — keep
it updated per the Documentation Requirements below regardless of this larger project.
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
AfskProfile, WavFileWriter) since these are pure protocol/DSP logic with no UI or
hardware dependency, matching this layer's "no UI, no radio model" character. No UI, no
radio model.
Layer 2 — RigModel: Transceiver class exposing clean C# properties and events. Consumes
CivEngine only. Also hosts the Phase 9 network layer (CivNetworkProtocol, CivTcpServer,
TcpCivTransport) since these implement/wrap ICivTransport, which is defined here.
Layer 3 — Services: Logger, EMMCOM bridge, ADIF logger, callsign lookup (Callook/QRZ/
HamQTH), LoTW bridge (via TQSL), HRD SQLite bridge, RadioInfo UDP broadcaster,
N1MM/WSJT-X UDP listener, SettingsService, NAudioPlayer/IAudioPlayer, AprsBeaconService,
PeriodicBeaconScheduler. Consume RigModel only.
Layer 4 — UI: Avalonia views and view-models. Consume Services and RigModel only. Never
touches CivEngine directly.
## Documentation Requirements
Two documents must be kept current, together, as a single discipline:
- CLAUDE.md (this file) — the developer/AI-facing project rules and phase status.
- UserManual.md (project root) — the end-user-facing manual covering everything a
  completed phase adds or changes for the person actually using the app. Currently
  functional and reasonably complete but NOT the exhaustive, screenshot-verified manual
  described above — see that section for the plan to eventually produce that version.
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
  (Rows="1") as the ItemsPanel, NOT Canvas positioning with a value converter — confirmed
  unreliable in Phase 7.
- FOR PER-ROW LOOKUPS AGAINST A VIEWMODEL-LEVEL COLLECTION: use IMultiValueConverter with
  a MultiBinding, not a single-value IValueConverter. See LotwConfirmedConverter (8d).
- FOR TOGGLEBUTTON BOUND TO BOTH IsChecked AND Command: set IsChecked's binding Mode to
  OneWay when the Command's handler itself sets the underlying property. Confirmed in
  Phase 10 (Auto Beacon button): binding IsChecked TwoWay (the default) while also having
  Command drive the same property caused the click to silently not toggle the visible
  state — the two bindings fought over who owned the value. OneWay makes the ViewModel
  (via the command) the single source of truth and the UI purely reflects it.
- EACH NEW ToggleButton NEEDS ITS OWN DEDICATED TEXT CONVERTER, not a reused one from an
  unrelated feature. Confirmed in Phase 10: reusing ContestModeButtonTextConverter for
  the Auto Beacon button displayed "Contest Mode: ON/OFF" text instead of Auto Beacon
  text. Build a small dedicated converter per toggle (see AutoBeaconButtonTextConverter)
  rather than reusing one written for a different toggle's specific wording.
- EVERY ToggleButton NEEDS THE FULL THREE-SELECTOR STYLE SET (:checked, :pointerover,
  :checked:pointerover), copied completely each time, not partially. Confirmed in Phase
  10: omitting :pointerover reintroduced the "invisible on hover" bug on a brand new
  button despite the base :checked style being present and correct.
- Environment.SpecialFolder.MyDocuments resolves to the OneDrive-redirected Documents path
  on this machine, not plain C:\Users\jrosp\Documents. Always verify actual file output
  location when debugging file I/O.
- Network-calling services must never throw back to the Transceiver's event dispatch —
  catch and record errors internally, never crash the polling loop.
- QsoLogger writes through to a persistent local ADIF file as each QSO is logged.
  COMPLETE — see Core Design Principle above.
- Neither IC-7300 nor IC-7300MK2 has a built-in TNC or APRS engine. APRS is built as
  software AFSK/AX.25 packet audio played out through an audio device. COMPLETE on
  Windows (NAudio/WASAPI) — see Phase 10. Real-world HF APRS tones (1600/1800 Hz, 300
  baud, matching DireWolf's actual deployed frequencies) are used, NOT the historical
  literal Bell 103 telephone-modem frequencies, which would not be decodable by real HF
  APRS listening stations — this distinction was specifically researched, not assumed.
- Custom-drawn UI controls sometimes need an explicit InvalidateVisual() call on the
  specific child element (e.g. the Image), not just the parent UserControl.
- Never reproduce copyrighted product photography as an asset in this project.
- HRD Logbook's SQLite schema is reverse-engineered from community sources — HrdSqliteBridge
  is defensive and purely a bonus layer on top of the always-reliable ADIF export path.
  COMPLETE — see Phase 8e.
- N1MM/WSJT-X/HRD UDP integration uses a public, documented XML-over-UDP protocol. One
  shared broadcaster/listener pair serves all three. COMPLETE — see Phase 8f.
- ICallsignLookupSource implementations must NEVER throw. COMPLETE — see Phase 8c.
- LoTW signing MUST be delegated to ARRL's own TQSL tool via ITqslProcessRunner — never
  reimplement ARRL's certificate/signing logic in-house. COMPLETE — see Phase 8d.
- Microsoft.Data.Sqlite (used by HrdSqliteBridge) transitively pulls a vulnerable native
  SQLite build via SQLitePCLRaw.lib.e_sqlite3 2.1.11 (CVE-2025-6965). Every project
  referencing Microsoft.Data.Sqlite MUST also explicitly pin SQLitePCLRaw.lib.e_sqlite3
  to 3.50.3 or later.
- Never store QRZ/HamQTH credentials, Phase 9's RemoteAuthToken, or any other secrets in
  AppSettings' backing JSON file within source control — settings.json is in .gitignore.
- LARGE CODE BLOCKS FROM CLAUDE CAN GET COPIED INCOMPLETE IF THE COPY BUTTON IS CLICKED
  BEFORE THE BLOCK HAS FULLY SCROLLED INTO VIEW. CONFIRMED FIX: scroll all the way to the
  bottom before clicking Copy. If a full-file replacement doesn't verifiably land on the
  first attempt (checked via findstr/dir), do not keep re-pasting into the same tab —
  switch immediately to File -> New File -> paste -> Save As -> Overwrite.
- WHEN PASTING A CODE SNIPPET, VERIFY IT LANDED IN THE INTENDED FILE. Check with `findstr`
  if a build error shows syntax from the wrong file type.
- WHEN MANUALLY EDITING A METHOD (not a full-file replace), re-view the resulting file
  before building if the edit is non-trivial — a partial/misplaced insertion can silently
  create a duplicate or malformed method header that produces confusing cascading
  compiler errors far from the actual mistake. Confirmed in Phase 10 (SendBeacon method).
- Phase 9's remote CI-V protocol requires a non-empty auth token — ValidateToken() rejects
  an empty expected token by design.
- Avalonia UI projects built with OutputType=WinExe will silently swallow
  Console.WriteLine output — fix via AttachConsole(ATTACH_PARENT_PROCESS) P/Invoke on
  Windows specifically, for any code path meant to run as a CLI tool.
## UI Design (flagged for future work, not yet scheduled as a phase)
User has indicated the current UI ("functional-first, each feature bolted on as its own
bordered box in a single scrolling window") is not satisfying and wants a real design
pass at some point. This decision (do the redesign, or consciously keep current layout)
is now also a prerequisite for starting the Comprehensive User Manual project above —
see that section. When redesign work starts, clarify with the user first: color
palette/theme direction, transceiver-panel aesthetic vs. modern flat dashboard, tabs vs.
one long scrolling window. Do not start a redesign without this input.
## Feature Priorities (build in this order)
Phase 1: CI-V engine + serial connection + frequency read/set + mode read/set — COMPLETE (23 tests)
Phase 2: Meter polling — COMPLETE (43 tests)
Phase 3: Avalonia UI — main panel — COMPLETE
Phase 4: Memory bulk editor — COMPLETE (52 tests)
Phase 5: Activity logger (CSV) — COMPLETE (56 tests)
Phase 6: EMMCOM dashboard integration — COMPLETE (60 tests)
Phase 7: Spectrum scope capture and waterfall display — COMPLETE (180 tests).
Phase 8: ADIF logging (general + contest + callsign lookup + LoTW + HRD + N1MM/WSJT-X) —
  COMPLETE, ZERO REMAINING ITEMS. All six sub-phases fully complete, engine and UI.
Phase 9: Remote/network mode — COMPLETE. Full client-server remote CI-V control pipeline
  (CivNetworkProtocol, CivTcpServer, TcpCivTransport, headless --headless-server launch
  mode) designed for LAN, VPN, or 44Net/AMPRNet, plus a full connection-mode Settings UI.
  REMAINING: real-world testing against actual IC-7300 hardware over a real network link
  (proven via localhost sockets and the demo transport to date, not yet against real
  hardware + a real network path).
Phase 10: APRS beacon over HF (formerly "Remote audio + APRS beacon") — WINDOWS COMPLETE,
  MACOS PENDING. Ax25FrameBuilder (spec-verified), AprsPositionFormatter (spec-verified,
  matches the official worked example exactly), AfskModulator/AfskProfile (bit-stuffing,
  NRZI, correct real-world HF tones), WavFileWriter (confirmed by actually listening to
  generated audio — sounds correct), NAudioPlayer/IAudioPlayer (WASAPI device selection,
  confirmed with real device enumeration), AprsBeaconService (PTT-keying with guaranteed
  release via try/finally — the critical safety piece, tested including the
  playback-throws-but-PTT-still-releases case), PeriodicBeaconScheduler (resilient to
  individual failures, tested), full Settings UI (callsign/SSID/symbol/position/audio
  device/interval), manual Send Beacon button and automatic Auto Beacon toggle — BOTH
  confirmed working live end-to-end on Windows, including real audio playing through a
  selected device and correct interval scheduling. 248 passing tests.
  REMAINING: macOS audio playback via AVFoundation (IAudioPlayer implementation) — not
  yet built. This requires native platform interop that cannot be authored or verified
  from a Windows session; must be built and tested directly on macOS. This is the ONLY
  remaining item in Phase 10.
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
- Do not trust a large paste without verifying it scrolled fully into view before the
  copy click, and that it landed in the intended file — switch to File -> New File ->
  Save As if a same-tab paste doesn't verifiably take on the first try
- Do not assume a prior session's file set is intact — run `dotnet build` at the start
  of any session
- Do not use Avalonia's CheckBox control — use ToggleButton instead
- Do not use Canvas + IValueConverter for evenly-spaced item layout — use
  ItemsControl + UniformGrid instead
- Do not use a single-value IValueConverter to check a row against a ViewModel-level
  collection — use IMultiValueConverter + MultiBinding instead
- Do not bind a ToggleButton's IsChecked TwoWay when its Command also drives the same
  property — use Mode=OneWay on IsChecked
- Do not reuse another feature's button-text converter for a new toggle — write a
  dedicated one
- Do not write a ToggleButton's style block with only some of the three required
  selectors (:checked, :pointerover, :checked:pointerover) — always all three
- Do not begin a UI redesign pass without first getting the user's input — see UI Design
  note above
- Do not begin the Comprehensive User Manual project until its trigger condition is met
  — see that section above
- Do not downgrade Avalonia to 11.x without new evidence — already researched and decided
- Do not run a CivTcpServer with a blank/empty auth token
- Do not assume Console.WriteLine output is visible in a WinExe-built app without
  AttachConsole
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
- APRS 1.0.1 spec (position report format): reference used in Phase 10, worked example
  !4903.50N/07201.75W- matched exactly in AprsPositionFormatterTests
- UserManual.md (project root): end-user documentation (functional version — see
  Comprehensive User Manual note above for the exhaustive version's plan)
## Related Projects
- APRS-Command (formerly CrossPlatformAPRS, KE4CON): originally the intended Phase 10
  beacon target; Phase 10 was ultimately built as a self-contained pipeline within
  IcomRigControl rather than bridging to APRS-Command.
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
7. Before any large paste: scroll to the bottom of the code block first. After any paste
   into an existing file: verify it landed correctly (findstr/dir); switch to
   File -> New File -> Save As if a same-tab paste doesn't verifiably take on first try
8. Never use CheckBox in new UI work — use ToggleButton
9. For evenly-spaced item layout, use ItemsControl + UniformGrid, not Canvas + converter
10. For per-row lookups against a ViewModel collection, use IMultiValueConverter +
    MultiBinding, not a single-value converter
11. For any new ToggleButton: dedicated text converter, IsChecked Mode=OneWay if a
    Command also drives the property, and the complete three-selector style set
12. Do not start the Comprehensive User Manual project until macOS audio is done AND
    the UI redesign question is explicitly resolved
## Deployment Targets
Headless CI-V server (Phase 9): Raspberry Pi 4 or 5, 2GB minimum, 4GB comfortable —
runnable via `IcomRigControl.UI --headless-server`.
Full Avalonia UI + scope on Pi: Raspberry Pi 5, 8GB RAM.
Storage: 16-32GB microSD (A2 rated recommended).
## Supported Desktop Platforms (all four, via Avalonia 12)
Windows: primary development platform — VS Code + GitHub Desktop
macOS: fully supported secondary/testing platform — Phase 10 audio work pending here
Linux desktop: full support
Raspberry Pi OS (Linux ARM64): Pi 5, 8GB
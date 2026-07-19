# IcomRigControl — Project Rules (CLAUDE.md)
## Project Identity
Name: IcomRigControl
Author: Jim, KE4CON
Language: C# (.NET 10)
UI Framework: Avalonia 12.x (cross-platform desktop — macOS, Windows, Linux, Raspberry Pi).
See "Avalonia Version Decision" below before considering any version change.
Target Radios: Icom IC-7300 (address 94h) and IC-7300MK2 (address B6h). IC-705 research
completed but not yet implemented — see "IC-705 Support Research" below.
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
## IC-705 Support Research (researched, not yet implemented)
User asked whether this project would work with an IC-705 in addition to the IC-7300/MK2.
Researched directly against the official IC-705 CI-V command reference PDF (see Key
References). Findings: default CI-V address is 0xA4 (vs. 0x94 for IC-7300). Core commands
we already use — 00/01/03/04/05/06 (frequency/mode), 07 (VFO), 0F/10/11 (split/tuning
step/attenuator), 14 (levels), 15 (S-meter/Po/SWR/ALC meters), 1C 00 (PTT), and the 27 xx
scope/waveform commands — are ALL present in the IC-705 with the same command/sub-command
numbers as the IC-7300. This means Transceiver's core logic (frequency, mode, PTT,
meters, waterfall) would very likely work against a real IC-705 with just the different
default address configured. NOT yet ported/verified: meter scaling formulas should be
checked byte-for-byte against the IC-705 manual rather than assumed identical (the
numbers look similar but this hasn't been confirmed with real hardware). GENUINELY NEW
territory, not a port: GPS/D-PRS commands (1A 05 02xx-03xx, 20 03, 23 xx) and D-STAR/DV
mode commands (17, 18, 1F, 20, 22) — the IC-705 has hardware/features the IC-7300 simply
doesn't have, so these would be new feature work, not adaptation of existing code. Also
worth noting: Icom's own RS-BA1 remote software officially supports the IC-705 including
its spectrum scope, per Icom's own compatibility chart — see RS-BA1 Comparison below —
which is a good real-world signal the IC-705's CI-V implementation is solid for this kind
of use. NEXT STEP WHEN PURSUING THIS: add IC705 to the RadioModel enum with address 0xA4,
verify meter scaling against real hardware, and decide whether GPS/D-PRS is in scope at all.
## RS-BA1 Comparison and Planned Additions (researched — Remote Power ON/OFF and Remote
## Audio identified as the two genuine capability gaps vs. Icom's own remote software)
Researched Icom's official RS-BA1 Version 2 product page and compatibility chart directly
(see Key References) to compare against IcomRigControl. RS-BA1 confirmed to support the
IC-7300 and IC-705 (including spectrum scope for both); the IC-7300MK2 is NOT listed in
RS-BA1's current official compatibility chart at all — this is unconfirmed as either a
genuine gap or just an outdated chart, and should be verified with Icom or the community
before assuming either way.
FEATURE COMPARISON (RS-BA1 v2 vs. IcomRigControl, based on Icom's own published
compatibility chart and feature descriptions):
- Remote CI-V control: both have it (IcomRigControl's Phase 9, proven via loopback,
  real-hardware test still pending).
- Remote real-time audio (listen/transmit live over IP): RS-BA1 has it; IcomRigControl
  does NOT — see "Planned: Remote Audio (Phase 12)" below.
- Dualwatch / dual scope: RS-BA1 only for IC-7851/7850/7610, not relevant to our target
  radios either way.
- Spectrum waterfall scope: both have it; IcomRigControl additionally has frequency axis
  labels and click-to-tune, which RS-BA1 does not advertise.
- Remote Power ON/OFF: RS-BA1 has it; IcomRigControl does NOT yet — see "Planned: Remote
  Power ON/OFF" below, a near-term addition.
- CW Keyer, Voice Recording/Playback, RC-28 USB dial hardware support: RS-BA1 has these;
  out of scope for IcomRigControl for now (no user request, low priority vs. logging/APRS work).
- QSO logging, callsign lookup, LoTW, HRD integration, N1MM/WSJT-X integration, APRS
  beacon, memory bulk editor, CSV activity logging, EMMCOM integration, headless Pi server
  mode, cross-platform (macOS confirmed working): IcomRigControl has ALL of these; RS-BA1
  has NONE of them — RS-BA1 is purely a CI-V/audio remote-control tool, not a logging or
  operations ecosystem. This is IcomRigControl's real, substantial differentiation.
- Cost: RS-BA1 is commercial (~$150 street price incl. required cable); IcomRigControl is
  free and self-extensible.
PLANNED: Remote Power ON/OFF (near-term, small, well-scoped addition). CI-V command 0x18
  (0x18 00 = off, 0x18 01 = on) is present in both the IC-7300 and IC-705 command tables.
  Transceiver/ICivTransport plumbing already exists — this needs one new CI-V command
  method plus a UI button (main dashboard, near PTT/mode controls). REAL LIMITATION to
  document for the user: 0x18 01 (power on) only works if the radio is in a low-power
  standby state with its CI-V listener still alive — it cannot power on a radio that is
  fully unplugged or hard-off. This is the same limitation RS-BA1 itself has, not a gap
  specific to our implementation.
PLANNED: Remote Audio (Phase 12 — deliberately scoped as its own future phase, NOT a
  quick addition). This is a genuinely different category of engineering from Phase 10's
  AFSK audio pipeline, which is one-shot (generate samples once, play them, done). Remote
  audio requires: continuous low-latency audio CAPTURE from the radio (not just playback
  — a new capability entirely, since IAudioPlayer only plays), a streaming protocol over
  the network (parallel to or extending CivTcpServer/TcpCivTransport, which currently
  only carries CI-V bytes, not audio), jitter buffering, and real latency tuning — plus
  the reverse path (mic input on the client, streamed to the radio's transmit audio
  input). Do NOT attempt to bolt this onto Phase 9 or Phase 10's existing code casually —
  plan it as a deliberate, dedicated multi-session phase when the time comes, likely
  requiring new interfaces (e.g. IAudioCapture alongside IAudioPlayer) and careful design
  before any code is written, given how different its real-time streaming demands are
  from every other Service built in this project so far, all of which are either
  request/response or one-shot.
## Comprehensive User Manual (planned, deliberately deferred — do not start early)
User has explicitly requested a professional, incredibly complete, step-by-step manual
with nothing left out (real screenshots, verified click-by-click steps, every known
quirk documented as troubleshooting). TRIGGER CONDITION to begin: macOS audio is DONE.
The remaining prerequisite is an explicit decision on the UI redesign question (see "UI
Design" note below): either it happens, or the user consciously decides to keep the
current layout and move on. Once triggered: dedicate session(s) specifically to this —
walk through every window in the running app together, capture real screenshots, verify
every instruction against live behavior, and fold in every known quirk as troubleshooting
entries. The existing UserManual.md remains the current, functional reference meanwhile.
## Cross-Platform Lesson: Platform-Specific Services Must Be Selected at EVERY
## Construction Site, Not Just One (learned the hard way, Phase 10 macOS session)
When a service has multiple platform-specific implementations behind a shared interface
(e.g. IAudioPlayer: NAudioPlayer for Windows, MacAudioPlayer for macOS), EVERY place in
the codebase that constructs that service must use the platform-aware selection, not just
the first place it was introduced. CONFIRMED REAL BUG: SettingsViewModel had a hardcoded
`new NAudioPlayer()` separate from MainWindowViewModel's correctly-fixed version, crashing
the entire app with an unhandled PlatformNotSupportedException the moment a macOS user
clicked Settings. NOT caught by the test suite (NAudioPlayerTests correctly skips on
non-Windows) — only surfaced by actually running the live app on macOS. LESSON: when
introducing a new platform-specific interface implementation, grep the whole codebase for
the OLD concrete type's name to find every construction site, and don't consider the
migration done until you've run the actual app on the non-default platform and clicked
through every window that might touch it.
## Avalonia Version Decision (researched and decided — do not revisit without new evidence)
This project stays on Avalonia 12.x. Researched and decided after the third confirmed UI
rendering bug (CheckBox, following the DataGrid and WaterfallControl Image repaint bugs).
Findings: the "control invisible until interaction" symptom is a real, filed upstream
Avalonia issue (AvaloniaUI/Avalonia#20726), explicitly reported as new to the 12.x line.
DECISION: stay on 12.x. Working, tested fixes exist for all known rendering bugs.
Revisit only if a new, genuinely unexplainable rendering bug appears.
## Architecture Layers (never mix concerns across layers)
Layer 1 — CivEngine: Raw CI-V framing, serial port I/O, BCD encode/decode, plus Phase 10's
AX.25/APRS/AFSK protocol layer. No UI, no radio model.
Layer 2 — RigModel: Transceiver class exposing clean C# properties and events. Consumes
CivEngine only. Also hosts the Phase 9 network layer (CivNetworkProtocol, CivTcpServer,
TcpCivTransport).
Layer 3 — Services: Logger, EMMCOM bridge, ADIF logger, callsign lookup, LoTW bridge, HRD
SQLite bridge, RadioInfo UDP broadcaster, N1MM/WSJT-X UDP listener, SettingsService,
IAudioPlayer (NAudioPlayer/MacAudioPlayer), AprsBeaconService, PeriodicBeaconScheduler.
Consume RigModel only.
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
- Events fired on the UI thread via Dispatcher.UIThread.InvokeAsync
- Nullable reference types enabled — no suppression without a comment explaining why
- Records for immutable data
- No magic numbers — all CI-V command bytes defined as named constants in CivCommands.cs
- All serial port access goes through ICivTransport interface to allow mocking in tests
- For DataGrid-style tabular UI: prefer ItemsControl + DataTemplate over Avalonia.Controls.DataGrid.
- AVOID Avalonia's CheckBox control — CONFIRMED BUG. Use a ToggleButton instead.
- FOR EVENLY-SPACED ITEMS ACROSS A CONTAINER'S WIDTH: use ItemsControl + UniformGrid
  (Rows="1"), NOT Canvas + IValueConverter.
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
- REAL-SOCKET/BACKGROUND-TASK TESTS SHOULD USE GENEROUS TIMEOUTS OR POLLING, NOT TIGHT
  FIXED DELAYS. Confirmed: TcpCivTransportTests.RadioDataReceived_IsRelayedToClientAs...
  flaked once under full-suite system load with a 2-second fixed wait across a five-hop
  async relay chain (radio transport -> CivTcpServer -> network -> TcpCivTransport read
  loop). Fixed with a 10-second timeout plus a clear failure message, and a polling loop
  for a sibling test with a shorter chain. Verified stable across multiple consecutive
  full-suite runs after the fix. When a test relays through more than one background
  task/real socket, prefer generous timeouts with clear failure messages, or polling,
  over a single tight fixed delay.
- Environment.SpecialFolder.MyDocuments resolves to a cloud-sync-redirected Documents
  path (OneDrive on Windows, potentially iCloud Drive on macOS) — always verify actual
  file output location when debugging file I/O, on either platform.
- Network-calling services must never throw back to the Transceiver's event dispatch —
  catch and record errors internally, never crash the polling loop.
- Neither IC-7300 nor IC-7300MK2 has a built-in TNC or APRS engine. APRS is built as
  software AFSK/AX.25 packet audio. COMPLETE on BOTH Windows (NAudio/WASAPI) and macOS
  (afplay via Process.Start) — see Phase 10.
- Custom-drawn UI controls sometimes need an explicit InvalidateVisual() call on the
  specific child element, not just the parent UserControl.
- Never reproduce copyrighted product photography as an asset in this project.
- HRD Logbook's SQLite schema is reverse-engineered — HrdSqliteBridge is defensive and
  purely a bonus layer. COMPLETE — see Phase 8e.
- N1MM/WSJT-X/HRD UDP integration COMPLETE — see Phase 8f.
- ICallsignLookupSource implementations must NEVER throw. COMPLETE — see Phase 8c.
- LoTW signing MUST be delegated to ARRL's own TQSL tool via ITqslProcessRunner.
  COMPLETE — see Phase 8d.
- Microsoft.Data.Sqlite transitively pulls a vulnerable native SQLite build via
  SQLitePCLRaw.lib.e_sqlite3 2.1.11 (CVE-2025-6965). Pin SQLitePCLRaw.lib.e_sqlite3 to
  3.50.3+ in every project referencing Microsoft.Data.Sqlite.
- Never store QRZ/HamQTH credentials, Phase 9's RemoteAuthToken, or any other secrets in
  AppSettings' backing JSON file within source control — settings.json is in .gitignore.
- LARGE CODE BLOCKS FROM CLAUDE CAN GET COPIED INCOMPLETE IF THE COPY BUTTON IS CLICKED
  BEFORE THE BLOCK HAS FULLY SCROLLED INTO VIEW. CONFIRMED FIX: scroll to the bottom
  before clicking Copy.
- ON WINDOWS: if a full-file replacement doesn't verifiably land on the first attempt,
  switch to File -> New File -> Save As rather than re-pasting into the same tab.
- ON MACOS: VS Code's Select All (Cmd+A) + Delete has been observed to NOT reliably clear
  editor content on this machine. Use terminal-based file writes instead: `cat > path 
  'EOF' ... EOF` for full-file writes, or `sed -i '' 's/old/new/'` for precise
  substitutions (note the empty '' after -i is required on macOS's BSD sed). ALSO
  CONFIRMED: even large heredoc pastes can silently fail to complete if not fully
  scrolled before copying — same root cause as the general large-paste issue, just
  showing up in a terminal heredoc instead of a VS Code paste. If a heredoc appears stuck
  waiting for more input (prompt shows `>` instead of returning), press Ctrl+C, verify
  nothing partial was written, and retry with a SMALLER chunk, not the same large one.
- WHEN VERIFYING WHETHER A LARGE FILE ACTUALLY WROTE COMPLETELY, don't just check a
  keyword count — check the file's line count/size against a reasonable independent
  estimate AND spot-check both the very beginning and the very end of the file. CONFIRMED
  REAL BUG (not a session artifact — found ALREADY BROKEN ON GITHUB): UserManual.md had
  been silently truncated to 86 lines, missing its entire first half (title through most
  of section 9), for an unknown prior period, undetected because verification checks had
  only ever grepped for specific keywords deep in the file, never checked total line
  count or the actual first line. Full rewrite was required. Always sanity-check total
  size/line count against expectation, not just "does this one phrase exist somewhere."
- WHEN PASTING A CODE SNIPPET, VERIFY IT LANDED IN THE INTENDED FILE. Check with
  `findstr` (Windows) or `grep` (macOS/Linux) if a build error shows syntax from the
  wrong file type.
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
  COMPLETE, ZERO REMAINING ITEMS. Includes ARRL Field Day and ARRL RTTY Roundup, both
  verified against official ARRL rules, selectable in the QSO Logger UI.
Phase 9: Remote/network mode — COMPLETE. REMAINING: real-world testing against actual
  IC-7300 hardware over a real network link.
Phase 10: APRS beacon over HF — FULLY COMPLETE ON BOTH WINDOWS AND MACOS. Confirmed live
  on both platforms including real audible playback. 259 passing tests, confirmed stable
  including a flaky-test fix (see Coding Standards above). NOTHING REMAINING.
Phase 11: Clickable radio front-panel control — user-photographed or vector image with
  transparent clickable regions overlaid. See UI Design note above. NOT STARTED.
Phase 12 (planned): Remote Audio — real-time low-latency audio capture/streaming/playback
  over the network, matching RS-BA1's core capability. See "RS-BA1 Comparison and Planned
  Additions" above for why this is scoped as its own deliberate phase, not a quick add.
  NOT STARTED — deliberately deferred, no code written yet.
Remote Power ON/OFF (planned, near-term, small — not yet assigned a phase number since
  it's a natural small addition to existing Transceiver/UI code rather than a new
  subsystem): CI-V command 0x18, see "RS-BA1 Comparison and Planned Additions" above.
  NOT STARTED.
IC-705 support (researched, not scheduled): see "IC-705 Support Research" above.
## What NOT to do
- Do not implement features out of phase order without explicit instruction
- Do not add NuGet packages without listing them here first
- Do not put CI-V logic in ViewModels
- Do not put UI code in CivEngine or RigModel
- Do not use Thread.Sleep — use Task.Delay with CancellationToken
- Do not swallow exceptions silently — log and surface them
- Do not hardcode radio addresses — read from config
- Do not store credentials/tokens in plain text in source-controlled files
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
- Do not trust a large paste without verifying it landed correctly — check size/line
  count against expectation, not just a keyword search, and check both the start and end
  of the file
- Do not rely on manual VS Code editing for file changes on macOS — use terminal
  heredoc/sed instead
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
  whole codebase for every construction site of the old one
- Do not use a single tight fixed delay for a test that relays through more than one
  background task or real socket — use a generous timeout with a clear failure message,
  or polling
- Do not begin a UI redesign pass without first getting the user's input
- Do not begin the Comprehensive User Manual project until the UI redesign question is resolved
- Do not downgrade Avalonia to 11.x without new evidence
- Do not run a CivTcpServer with a blank/empty auth token
- Do not assume Console.WriteLine output is visible in a WinExe-built app without AttachConsole
- Do not attempt Remote Audio (Phase 12) as a casual addition to existing Phase 9/10 code
  — plan it as its own dedicated phase, see "RS-BA1 Comparison and Planned Additions" above
## Radio Addresses
IC-7300: 0x94 (controller default: 0xE0)
IC-7300MK2: 0xB6 (controller default: 0xE0)
IC-705: 0xA4 (researched, not yet implemented — see IC-705 Support Research above)
## Key References
- IC-7300MK2 CI-V Reference Guide: icomuk.co.uk/files/icom/PDF/productAdditionalFile/IC-7300MK2_ENG_CI-V_0.pdf
- IC-705 CI-V Reference: icomeurope.com/wp-content/uploads/2020/08/IC-705_ENG_CI-V_1_20200721.pdf
- RS-BA1 Version 2 product/compatibility page: icomamerica.com/lineup/options/RS-BA1_Version2/
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
- APRS 1.0.1 spec: worked example !4903.50N/07201.75W- matched exactly in
  AprsPositionFormatterTests
- ARRL RTTY Roundup rules: contests.arrl.org/ContestRules/RTTY-RU-Rules.pdf
- macOS afplay reference: ss64.com/mac/afplay.html
- UserManual.md (project root): end-user documentation
## Related Projects
- APRS-Command (formerly CrossPlatformAPRS, KE4CON): originally the intended Phase 10
  beacon target; Phase 10 was ultimately built as a self-contained pipeline within
  IcomRigControl instead.
- EMMCOM Field Comms Server: Phase 6 — COMPLETE
- Ham Radio Deluxe: user's PRIMARY day-to-day logger — Phase 8e COMPLETE
- N1MM Logger+: Phase 8f COMPLETE
- WSJT-X: Phase 8f COMPLETE
- RS-BA1 (Icom's own remote control software): see comparison above — not a codebase we
  interact with, but a useful feature-parity reference point.
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
   heredoc/sed for file writes, in SMALL chunks (large heredocs can also fail to paste
   completely) — see Coding Standards.
8. Never use CheckBox in new UI work — use ToggleButton
9. For evenly-spaced item layout, use ItemsControl + UniformGrid, not Canvas + converter
10. For per-row lookups against a ViewModel collection, use IMultiValueConverter + MultiBinding
11. For any new ToggleButton: dedicated text converter, IsChecked Mode=OneWay if a
    Command also drives the property, and the complete three-selector style set
12. Do not start the Comprehensive User Manual project until the UI redesign question is resolved
13. When adding or modifying a platform-specific service, grep the whole codebase for
    every construction site of the type being replaced
14. When verifying a large file wrote correctly, check size/line count against
    expectation and spot-check both the start and end — not just a keyword search
15. For tests relaying through multiple background tasks/real sockets, use generous
    timeouts with clear failure messages, or polling — not a single tight fixed delay
## Deployment Targets
Headless CI-V server (Phase 9): Raspberry Pi 4 or 5, 2GB minimum, 4GB comfortable —
runnable via `IcomRigControl.UI --headless-server`.
Full Avalonia UI + scope on Pi: Raspberry Pi 5, 8GB RAM.
Storage: 16-32GB microSD (A2 rated recommended).
## Supported Desktop Platforms (all four, via Avalonia 12)
Windows: primary development platform — VS Code + GitHub Desktop
macOS: fully supported secondary platform — Phase 10 audio COMPLETE here too, confirmed
live. VS Code manual editing has been observed unreliable — use terminal heredoc/sed
instead, in small chunks.
Linux desktop: full support
Raspberry Pi OS (Linux ARM64): Pi 5, 8GB
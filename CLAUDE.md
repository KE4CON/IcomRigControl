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
Layer 3 — Services: Logger, EMMCOM bridge, APRS beacon, backfill queue. Consume RigModel
only.
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
## Feature Priorities (build in this order)
Phase 1: CI-V engine + serial connection + frequency read/set + mode read/set
Phase 2: Meter polling (S-meter, SWR, ALC, power, voltage, current)
Phase 3: Avalonia UI — main panel with frequency display, mode selector, meter gauges
Phase 4: Memory bulk editor (read all 99 channels, edit in DataGrid, write back)
Phase 5: Activity logger (CSV output, frequency/mode/meter timestamped)
Phase 6: EMMCOM dashboard integration (push rig status to Field Comms Server)
Phase 7: APRS beacon (beacon operating frequency as APRS object via CrossPlatformAPRS
bridge)
Phase 8: Spectrum scope capture and waterfall display
Phase 9: Remote/network mode (headless Pi server + TCP client)
Phase 10: Remote audio (NAudio on Windows; AVFoundation wrapper on macOS)
## What NOT to do
- Do not implement features out of phase order without explicit instruction
- Do not add NuGet packages without listing them here first
- Do not put CI-V logic in ViewModels
- Do not put UI code in CivEngine or RigModel
- Do not use Thread.Sleep — use Task.Delay with CancellationToken
- Do not swallow exceptions silently — log and surface them
- Do not hardcode radio addresses — read from config
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
## Related Projects
- CrossPlatformAPRS (KE4CON/CrossPlatformAPRS): APRS beacon target for Phase 7
- EMMCOM Field Comms Server: dashboard integration target for Phase 6
## Session Start Checklist
Before writing any code in a session:
1. Read this file
2. Confirm which Phase is active
3. Check that the layer being touched matches the Phase
4. Do not refactor other layers unless the current Phase explicitly requires it

## Deployment Targets
Headless CI-V server (Phase 9, no UI): Raspberry Pi 4 or 5, 2GB minimum, 4GB comfortable.
Full Avalonia UI + scope on Pi (Phase 8-9 combined): Raspberry Pi 5, 8GB RAM — standardized target for breathing room with scope waterfall, EMMCOM bridge, and APRS beacon running concurrently.
Storage: 16-32GB microSD (A2 rated recommended for sustained write performance from ActivityLogger).
Desktop development target: MacBook Pro M1 Pro or better, macOS 26+.

## Supported Desktop Platforms (all four, via Avalonia 11)
macOS: primary development platform, M1 Pro, macOS 26+
Windows: full support for EMMCOM members and any Windows-based go-kit laptops
Linux desktop: full support (Ubuntu/Debian primary test targets)
Raspberry Pi OS (Linux ARM64): Pi 5, 8GB — see Deployment Targets above
All four platforms build from the same Avalonia 11 UI project — no platform-specific UI code unless a capability is genuinely unavailable, in which case isolate behind ICivTransport per architecture rules.
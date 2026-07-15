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
RadioInfo UDP broadcaster, N1MM/WSJT-X UDP listener. Consume RigModel only.
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
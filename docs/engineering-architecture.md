# LINK360 engineering architecture and implementation gates

Adopted October 13, 2026 from the user's architecture specification v1.0.0.
Read alongside `product-direction.md` and the investigation records.

## Purpose and production direction

Build a local controller for Insta360 Link, initially Windows 11, with later
macOS/Linux transport adapters. Target baseline: controller 2.2.5.14 and
user-reported camera firmware v1.4.8_build2. Firmware is not independently read
by LINK360 yet.

Production direction: React/TypeScript UI with a narrow Tauri IPC boundary and
a Rust engine owning hardware, settings, action state and OS integration.
The UI receives capabilities and explicit results; it does not access hardware,
arbitrary files or network sockets. Python and REA are optional investigation
tools, not required runtime services. The existing C#/PowerShell prototype is
an evidence-producing implementation to preserve during any Rust migration.

Runtime privacy: local settings and processing, no telemetry, accounts, cloud
dependencies or uploaded footage. Optional diagnostics exclude frames and
sensitive identifiers. Audit dependencies and the installed driver stack before
claiming zero network egress for the complete system.

## Current evidence and transport decision

The connected camera's Windows inventory reports VID 2E1A / PID 4C01 and an
OK camera interface. This identifies hardware; it does not establish commands.
See `investigations/direct-camera-control.md` for the newer prototype evidence:
IAMCameraControl range/current-value reads and zoom writes/readback/restoration
have succeeded. Physical motion and visual zoom were subsequently verified in
OBS; see `investigations/visible-motion-and-desktop.md` for the desktop prototype
and the remaining evidence gates.

Use supported Windows camera controls first. Investigate IKsControl extension
units for capabilities missing from standard controls. nusb is a candidate raw
USB adapter, not a mandatory transport: its Windows WinUSB requirements must
be reconciled with the camera's installed driver and video use. No driver
replacement or speculative USB writes are authorized by a library preference.

The supplied Rust blueprint is illustrative: it filters only by VID, selects
the first match, ignores zoom, and does not transmit commands. Its opcodes,
payload layout and endianness are unverified. Do not reuse them as a protocol.
Cargo/rustc were not found on the inspected shell PATH; this does not establish
that Rust is absent everywhere. Verify compiler, linker and SDK before a port.

## Staged component contracts

| Component | Inputs and outputs | Evidence gate |
| --- | --- | --- |
| Discovery | OS inventory to explicit device identity/capabilities | Multiple/no devices, access errors, unplug/reconnect |
| Control adapter | Validated action in documented units to result/readback | Range/step/flags, transport errors, physical behavior |
| Protocol encoder, if needed | Proven request fields to bytes | Captured/static evidence plus representative hardware validation |
| Native control engine | Actions and connection events to cancellable state | Stop, deadlines, disconnect, contention, stale-action rejection |
| Tauri/UI | Validated IPC commands to capability/state/result events | Engine contract established; no arbitrary native access |
| Presets/hotkeys/macros | Local configuration and triggers to bounded actions | Persistence, cancellation, manual override and hidden-UI tests |
| Video/OBS/Discord | Existing camera output and supported integrations | Individual and concurrent-use tests before routing decisions |
| Local AI | Local frames to bounded movement actions | Stable controls, local processing, pause/override and limits |

Active work remains discovery, direct controls and focused REA protocol
investigation. Continue existing zoom validation and extension-unit analysis.
Do not hold proven standard controls hostage to proprietary protocol recovery.
Advance to UI integration after the independently usable control engine passes
its gates; video routing and AI remain later work.

## Control and failure requirements

Select the exact device; ambiguous matches must return an explicit error.
Expose supported controls, native units, limits, step and automatic/manual flags.
Reject invalid actions before writes. Separate requested, last confirmed and
unknown state; never present the last command as measured camera coordinates.

Bound operations with deadlines. Disconnect cancels queued and active actions,
invalidates current state, and starts bounded exponential-backoff discovery.
Reconnection must not replay stale movement automatically. Verify these paths
before claiming reconnect support or predictable timing.

UI failure and native failure are separate events. Rust cannot simply catch a
webview crash as a Rust panic. Define lifecycle handling and test continued
control, stop behavior and atomic recovery persistence explicitly. Record only
last confirmed state, marking it unknown when no measurement is available.
Tauri architecture alone does not prove process isolation or deterministic
latency; measure latency under representative load and document limits.

## Validation and contract changes

Record command/API inputs, state transitions, boundaries, outputs and evidence
locations under `investigations/`. Keep observed facts separate from inference.
For recovered commands, establish request type, request, selector/unit/interface,
payload size, byte order, units, ranges, responses and stop semantics.

Required behavior checks: no/ambiguous device, invalid range/step, supported and
unsupported controls, readback mismatch, visible movement/zoom, stop/manual
override, access failure, unplug/reconnect and competing applications. Run tests
appropriate to the implementation stage; do not call future gates completed.

Map downstream callers and persisted data when changing a contract. Retain
versioned encoders only when distinct firmware contracts are evidenced; unknown
firmware/capabilities must not receive guessed commands. Preserve notices for
reused source. Do not commit credentials or sensitive capture data.

## Primary transport references

- https://docs.rs/nusb/latest/nusb/
- https://learn.microsoft.com/en-us/windows-hardware/drivers/stream/device-requirements-for-usb-video-class-extension-units
- https://learn.microsoft.com/en-us/windows/win32/api/strmif/nn-strmif-iamcameracontrol

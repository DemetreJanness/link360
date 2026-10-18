# LINK360 product direction

## Purpose

Build a reliable local controller for Insta360 Link cameras with smoother
control, flexible hotkeys, programmable actions, and eventual local AI tracking.
The user's primary applications are both OBS and Discord.

## Intended behavior

- Reliable camera discovery, reconnect handling, and manual pan/tilt/zoom.
- Named presets, smooth movement where supported, and image controls.
- Hotkeys and cancellable action sequences with manual override.
- OBS coordination for scene changes and camera actions.
- Camera output usable in Discord, with camera actions and hotkeys useful
  during Discord calls as well as OBS sessions.
- Investigate concurrent OBS/Discord use and whether a shared virtual-camera
  output is needed. Do not assume simultaneous direct hardware access works.
- Optional local AI tracking with framing controls, movement limits, and
  tracking pause/resume.

A candidate kill-cam action moves to a close-up, optionally switches an OBS
scene, waits, and restores previous framing. Camera-only actions should also
work during Discord calls without requiring OBS. Define Discord-specific
automation only after its supported integration mechanisms are verified.

## Privacy requirements

No accounts, analytics, telemetry, uploaded footage, or user-data tracking.
Store settings locally. Diagnostic logging is optional and excludes camera
frames. Process AI tracking locally. Any future cloud feature requires an
explicit user choice. Audit network behavior of dependencies and integrations.

## Implementation sequence

Apply `engineering-architecture.md` to this sequence. Preserve verified native
prototype behavior while establishing the intended Rust/Tauri production core.
Transport selection follows evidence from the installed camera interfaces;
raw USB is conditional on demonstrated need and driver compatibility.

1. Establish independently usable camera discovery and control commands.
2. Build reliable control state, reconnect behavior, presets, and hotkeys.
3. Add cancellable task/action sequences.
4. Validate video output in OBS and Discord, including concurrent use.
5. Add supported OBS integration and local AI tracking.

These are product requirements and investigation goals, not implemented or
verified capabilities. Hardware transport, video routing, and integration
details remain to be established.

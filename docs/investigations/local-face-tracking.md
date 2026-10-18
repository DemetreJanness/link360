# Local face tracking

Target: LINK360 C# prototype, Insta360 Link standard UVC pan/tilt, OBS 32.1.0
raw dshow source. Date: October 13, 2026. Question: add local AI tracking while
preserving shared OBS/Discord video and manual priority.

## Implementation and boundaries

Existing source and supported OBS RPC were inspected first. This extends the
REA-derived standard-control investigation; proprietary USB commands and further
REA decompilation provide no additional evidence needed for this implementation.

`ObsVideoRoute.TrackingFrame` reads the verified, enabled and visible Link source
over authenticated loopback. It does not open another camera capture session or
change the OBS virtual camera output. Images stay in memory and local stdin.
`LocalTracking.cs` runs YuNet CPU inference in a separate local worker, retains
the model while enabled, validates its SHA256, and gives each inference a 3s
deadline. Cancellation/failure terminates the worker. No runtime model downloads,
remote endpoints, telemetry, identity recognition or face database are added.

OpenCvSharp core and Windows runtime are pinned to 4.13.0.20260627. Model source,
commit and checksum are in `models/provenance.json`; model license is beside it.
Dependency licenses are in `docs/licenses/`. The initial all-in-one package was
replaced with core/runtime references to avoid unnecessary WPF dependencies.
NuGet vulnerability metadata lookup was unavailable in this environment; package
restoration and local inference succeeded, but a vulnerability audit is pending.

`TrackingPlanner` requires exactly one confidence >=0.85 face, valid bounds and
two spatially consistent detections. It holds on missing/ambiguous faces, keeps
zoom unchanged, ignores center error <3.5% and bounds each correction to 5 driver
units (or the hardware step). UI limits session travel to 20 pan / 15 tilt units.
No blind search/reacquisition movement is issued. Commands use the existing
validated eased worker and native ranges. Duplicate OBS frames stop tracking.

Ctrl+Alt+Shift+T toggles tracking. Manual nudges/applies/presets/previous-framing
recall cancel inference and ongoing tracking movement before reading fresh state
and executing the manual action. End and Cancel stop it. Device changes stop it;
reconnect never re-enables it. Changing reverse-axis options also cancels it.

## Evidence

`node scripts/check-tracking.mjs` performed real local inference against an OBS
frame without writing images: first invocation 1159ms, one face; a later cold
invocation 612ms, zero faces. Virtual Camera reported active throughout. Desktop
hotkey toggled tracking on; face detection and changed pan/tilt readbacks were
observed. End stopped it and suspended hotkeys. The user initially answered the
following test negatively, then clarified they were not looking and requested a
repeat; that initial response is not evidence of reversed tracking directions.

With the user holding still, `node scripts/calibrate-tracking.mjs` performed
small +8 pan and +6 tilt tests, comparing median detections and restoring the
starting pose in finally. Face displacement measured -11.94 horizontal pixels
per positive pan unit and +11.20 vertical pixels per positive tilt unit. This
supports the default centering directions for this raw OBS source. Only numeric
deltas were saved under ignored `.rea-tools/`; no frames were saved.

Tests in `tests/Program.cs` cover acquisition, missing/multiple/invalid detections,
deadband, direction reversal, hardware range bounds and unchanged zoom, alongside
the existing movement and OBS route checks. `scripts/check-desktop.ps1` passed
real hardware worker protocol, invalid commands/ranges and preset persistence.

The repeat used a persistent inference worker instead of reloading the model
each frame. The user confirmed: follows, manual nudge stops tracking, video live.
The panel independently showed face detections, changing pan/tilt with Zoom127
unchanged, then `Local AI tracking off · manual override` and fresh OBS frames.
The hotkey also now waits for background reads rather than rejecting them as
busy; a pending enable is invalidated by cancellation/manual action. Worker
cancellation registers a direct process termination callback so UI shutdown
does not require a UI continuation to terminate inference. Tracking is left off.

## Responsiveness revision

The user subsequently reported that following was slow and not fluid. The first
loop waited for 500ms moves plus a 350ms fixed pause and camera process startup.
The revised loop uses 150ms trajectories, a 50ms pause, a 3.5% deadband, and
proportional corrections capped at 5 units. A persistent camera worker holds one
verified COM binding only for the enabled session and still validates ranges,
steps, manual flags and endpoints for each motion. CameraEngine serializes it
with other requests; manual override kills the session before a fresh manual
read. Background camera polling pauses while tracking owns that binding.

A live 3-unit correction measured 296ms roundtrip with worker reuse alone, then
207ms with binding reuse. Short-trajectory, worker reuse, cancelled-session
rejection, subsequent native reads and restoration passed. These are control
roundtrips, not end-to-end optical tracking latency. UI reports update timing.
Preset UI remains 0.5–6 seconds; the native trajectory contract now accepts
150–6000ms so short tracking corrections use the same rate-bounded planner.

Further profiling isolated the dominant delay to OBS PNG encoding. The original
640-pixel PNG requests measured 291–374ms; 320-pixel PNG requests took 73–104ms;
320-pixel JPEG quality80 took 7–15ms. Tracking now uses 320x180 JPEG, a detector
input matching that size, 75ms requested trajectories (still lengthened by the
existing per-axis rate bounds) and a 15ms loop pause. Health checks retain their
160x90 PNGs. OBS/Discord capture/output resolution is unchanged. The native
trajectory minimum is now 75ms; preset UI still starts at 500ms.

## Limits and second-camera direction

Driver readbacks do not alone prove optical following; user-visible follow and
manual-override validation results must be recorded separately. This is slow
face-centering, not identity tracking, full-body AI or calibrated velocity.
Camera movement, capture and inference add delay; no gaming-load guarantee exists.

The user proposed using both cameras. A fixed Acer overview could improve lost
target detection, but mapping its image into Link pan/tilt requires a measured
cross-camera calibration and target association. This remains proposed. Do not
drive the Link from uncalibrated Acer coordinates or activate its hidden OBS
source automatically. The frame-provider boundary permits that later extension.

Primary references: [OpenCV YuNet](https://github.com/opencv/opencv_zoo/tree/main/models/face_detection_yunet),
[OpenCvSharp source](https://github.com/shimat/opencvsharp),
[OBS Virtual Camera](https://obsproject.com/kb/virtual-camera-guide).

# Shared video, reconnect recovery and smooth presets

Target: Insta360 Link firmware v1.4.8_build2; installed Windows UVC driver;
local OBS and Discord 1.0.9261. Investigation date: 2026-10-13.

Question: Can LINK360 control the hardware with the vendor app closed while OBS
and Discord share video, recover USB reconnection, and recall smoother presets?

## Evidence and conclusions

The earlier REA/static investigation established the standard Windows camera
control boundary. This task extends that observed route, rather than sending
unverified proprietary USB commands. REA is not a runtime dependency.

OBS's `Main Camera - SELECT INSTA360 LINK` dshow input was checked against
VID 2E1A/PID 4C01. OBS virtual camera status reports active. Discord's More Camera
Options menu shows OBS Virtual Camera; the user confirmed that their own tile
updates with the live camera image. The desktop helper's screenshot returned a
different window, so live Discord pixels are user-confirmed, not independently
visually observed. No new call was started during verification.

The user unplugged/reconnected the USB cable. The missing-device interval was
not independently sampled in that first test. Afterwards, native controls read
successfully but OBS returned the identical frozen image over repeated reads
despite an active source. Its SHA256 was
`2E9EEEEA91625177691B7D82DB5147A3F0152828B115B13F1ABA204381871410`.
Deactivation/reactivation via `PressInputPropertiesButton`, property `activate`,
restored visually live frames and differing frame hashes. This established that
successful control reads alone cannot establish video health.

`src/desktop/ObsVideoRoute.cs` now checks tiny 160x90 PNG previews in memory every
two seconds. Repeated identical frames trigger source recovery, no more than
three times per stalled episode, spaced by at least ten seconds. Device changes
also request recovery after the native camera returns. Disabled/hidden sources
are respected. Authentication and connections stay on 127.0.0.1; no previews,
tokens or camera identifiers are saved. Windows device inventory notifications
conservatively invalidate state even for unrelated device changes. Old movement
is discarded rather than replayed after reconnection.

`src/native/SmoothMotion.cs` plans eased quantized targets, validates the whole
pose before writes, limits planned axis rates and skips late intermediate points.
One binding/worker spans each movement. `scripts/test-smooth.ps1` measured 38
changed-axis commands over 1415ms for a 1400ms transition, and 54 over 3013ms for
a 3000ms transition. Before/middle/end OBS images showed intermediate framing.
Cancellation interrupted the transition; original Pan9/Tilt0/Zoom127 was restored.
An additional live CameraEngine test verified cancellation kills its worker,
allows subsequent reads and restores framing.

## Validation commands

- `dotnet run --project tests/Link360.Tests.csproj -c Release -- --live-engine`
  passed planner endpoints, monotonicity, quantization, bounds, route recovery,
  disabled/hidden/wrong-device handling, retry limits and live cancellation.
- `./scripts/check-desktop.ps1` passed worker protocol, invalid command/range
  rejection and saved Main preset persistence.
- `./scripts/test-smooth.ps1 -DurationMs 1400` and `-DurationMs 3000` passed live.
- `node scripts/obs-camera-route.mjs status` verifies local video route status.

## Monitored physical repeat

With the new watcher running, `node scripts/monitor-reconnect.mjs` independently
sampled camera disappearance from 04:40:15 through 04:40:28 UTC on 2026-10-14.
Reads resumed at 04:40:30; OBS initially retained the disconnect frame, then its
hash changed at 04:40:33 and continued changing. Virtual Camera remained active
throughout. LINK360 displayed Connected and Fresh OBS frames. No manual source
repair or preset recall was issued during this repeat. The user confirmed that
Discord's image resumed automatically without reselecting the camera. This
validates end-to-end recovery in this observed run; the monitor alone cannot
attribute the recovery to a specific OBS restart call. Numeric samples and
ephemeral frame hashes are in ignored `.rea-tools/reconnect-monitor.json`.
The camera briefly reported reset/calibration positions before settling back
to Pan9/Tilt0/Zoom127; LINK360 did not issue motion commands during the monitor.

## Limits

Hung drivers, multiple matching cameras and gaming-load latency need broader
hardware testing. Cancellation prevents future commands, not a guaranteed motor
hard stop. Readbacks are driver state rather than calibrated optical position.
An unchanging preview is a heuristic, so retries are bounded and manual repair
is available. OBS virtual camera composition follows the user's OBS configuration.

Primary references: [OBS Virtual Camera guide](https://obsproject.com/kb/virtual-camera-guide)
and [OBS Windows dshow source](https://github.com/obsproject/obs-studio/blob/master/plugins/win-dshow/win-dshow.cpp).

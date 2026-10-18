# Recorded motion and latency investigation · 2026-10-14

## Target and question

LINK360 software tracking on original Insta360 Link, firmware 1.4.8_build2,
fixed Acer overview, ten saved room points. Investigate stationary jitter,
stepped following and excessive reframing in the user's two local recordings.
Keep existing map, local processing, manual override and OBS/Discord routes.

## Evidence and limits

- `%USERPROFILE%/Videos/2026-10-14 01-36-49.mp4`: 1920×1080, 60 FPS,
  2,564 frames, 42.733 seconds. PTZ output; nearly still initially, walking
  around 18–19 seconds, head cropping near 31 seconds.
- `%USERPROFILE%/OneDrive/Desktop/acer.mp4`: 282×216, 30 FPS, 779 frames,
  25.967 seconds. Acer preview recording includes overlays, not raw capture.
  The room view often retains the subject; a zero-person caption appears near
  11.8 seconds. Clips lack synchronized timestamps and corresponding command
  logs. Their exact temporal alignment and cause of each movement are unproven.
- Ignored `.rea-tools/video-review/` contains local contact sheets and numeric
  replay results. Development review of user-supplied video is separate from
  runtime logging; no frames are added to application reports.
- Static controller tracing found fine absolute goals based on parent-side
  cached driver positions. The motor can pass that position before the next
  correction, making the next old-position goal point backwards. This is a
  reproducible code-level failure mechanism, not proof it caused each video jerk.
- Small bounding-box movements could repeatedly cross the old .035 deadband.
  Bounding-box centers also change with arms/occlusion independently of torso.

## Implementation

- `AdaptiveAim.cs`: adaptive coordinate filtering; cutoff increases with
  motion speed. Axis hysteresis starts corrections at .065 normalized error,
  ends at .035. Removed forced minimum correction below driver resolution.
- `LocalTracking.cs`, native continuous worker: bounded relative fine deltas
  rebase on latest issued pose under the command gate. Acer goals use the full
  calibrated absolute target, with speed/acceleration/quantization bounds per
  servo tick. No calibrated-area extrapolation. Snapshot expiry is 500 ms;
  unreplenished demand expires after 350 ms. Single-camera travel remains bounded.
- `HybridTrackingPlanner.cs`: fresh unique Link torso can continue without
  Acer. Distinct-frame acquisition and short handoff hold prevent repeated old
  frames from qualifying a lock. Acer recovery requires fresh unique detections
  and both raw/filtered coordinates inside the map hull.
- `PoseGesture.cs`: shoulder/hip torso anchor on the Link using existing pinned
  local pose model. Missing shoulders prevents head-only acquisition. Acer
  coordinates and all ten existing map points are unchanged.
- `ControllerForm.cs`, `DirectVideo.cs`: wake control on new frames; timestamp
  direct capture immediately after OpenCV read, before JPEG/inference. This is
  application read time, not camera exposure time. OBS fallback uses receipt time.
- Serial detector workers reuse large input buffers, avoid copying the whole
  YOLOX output, request only needed pose outputs. No model changes/downloads.
- `RoomMapTest.cs`: numeric schema 2 includes commanded/resolved targets,
  anchor type, snapshot age, relative deltas and speed. Driver/command positions
  are not physical motor encoders. No images, joints or identities are logged.

## Validation

Commands (from workspace):

```powershell
dotnet build tests/Link360.Tests.csproj -c Release -o .rea-tools/jitter-tests --no-restore
.rea-tools/jitter-tests/LINK360.Tests.exe --saved-map
.rea-tools/jitter-tests/LINK360.Tests.exe --replay-video --pose-anchors --profile '%USERPROFILE%\Videos\2026-10-14 01-36-49.mp4'
node scripts/check-continuous-motion.mjs .rea-tools/jitter-tests/LINK360.exe --relative
```

All policy/motion/map/logging/override/reconnect fixtures pass. Stationary
synthetic noise gives zero commands; a fast displacement responds within one
33-ms planner fixture frame. This excludes capture/inference/USB/motor latency.
Ten saved points reproduce their exact PTZ positions; map limit remains 4–25.

Six-second GPU replay, 61 samples, first five excluded from timing/allocation:

| Measurement | Before buffer reuse | After |
| --- | ---: | ---: |
| Mean body + pose pipeline | 27.26 ms | 20.39 ms |
| P95 pipeline | 47.65 ms | 40.26 ms |
| Managed allocation/frame | 17,324,884 bytes | 13,174 bytes |
| Usable torso detections | 61 | 61 |
| Quiet correction-triggering samples, old/new planner | 43 / 0 | 43 / 0 |

The same anchor coordinates/noise were produced before/after. Timing is one
local sequential benchmark, excludes video seek/JPEG creation and native/GPU
allocations, and can vary with load. It is not end-to-end hardware latency.

Twice, a bounded standard-UVC device test issued four one-unit relative pan
corrections with intentionally stale absolute goals. Driver readings progressed
monotonically [-2,-1,0,1,2,2,…], held steady, preserved tilt/zoom and restored the
starting position. Vendor app was closed and native tracking off. This verifies
small driver-level commands, not subjective whole-room smoothness.

Live tracking feel, torso framing, Acer handoff, new calibration captures and
OBS/Discord concurrency still need a person present. Automatic native restart
remains gated; see native-tracking-usb.md. More valid points improve the spatial
map; extra points do not replace temporal noise filtering.

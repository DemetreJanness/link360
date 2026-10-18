# Shared Link tracking pipeline · 2026-10-18

## Target and question

LINK360 C# prototype at commit `1cc6b3c`. Review the local tracking path for a
low-risk way to reduce delayed observations and unnecessary vision work without
changing camera command units, room calibration, model thresholds or privacy
boundaries.

## Static evidence

- `WatchLinkPreview` owns a persistent Link body/head inference worker whenever
  the two-camera preview is running.
- Room tracking already consumes that worker's latest observation through
  `linkObservation` and wakes on `trackingFramesReady`.
- Link-only `Track` previously created another body/head worker and requested
  another Link frame even when `WatchLinkPreview` was active. Those calls share
  the same `DirectFrames` gate, so the two consumers alternated capture requests
  and ran equivalent inference in separate processes.
- The standalone path is still required when the user has not started previews.
- OBS fallback frames have receipt times rather than camera-read times. Before
  this change, `WatchLinkPreview` could continue running inference and publish a
  fresh receipt time for an unchanged fallback image.

These are source-level findings. No Windows camera, GPU or motor was available
in this review environment, so the physical latency and smoothness effect has
not been measured here.

## Implementation

- Link-only tracking now consumes the existing preview observation when that
  preview is active. It processes each observation once and waits for the next
  frame signal instead of polling and duplicating inference.
- If the preview is stopped or fails, tracking lazily creates the original
  standalone inference worker and continues through the existing direct/OBS
  feed path. If the preview later becomes available, the redundant worker is
  disposed.
- Observations older than 600 ms are held rather than fed into the controller.
  The existing manual override, travel bounds, deadband, feedback budget and
  continuous-camera worker remain unchanged.
- An unchanged OBS fallback image is marked unavailable after two seconds and
  is no longer sent repeatedly through inference. Direct-camera frames retain
  their worker-provided read timestamp and do not incur this fallback hash.

## Expected effect and limits

With previews active, Link-only mode should use one Link vision worker instead
of two and should avoid alternating duplicate capture requests. This reduces a
known source of GPU/CPU work and queueing, but it does not establish a frame-rate
or latency improvement until measured on the Windows hardware. It also does not
change the camera's whole-unit PTZ resolution, motor behavior or the 260–300 ms
optical-feedback estimate from earlier recordings.

## Validation

- `git diff --check` passes.
- Static inspection confirms the preview-active branch reuses
  `linkObservation`, deduplicates by observation time and disposes any fallback
  worker before continuing.
- Static inspection confirms the preview-off branch still creates
  `LocalBodySession`, reads `feeds.LinkFrame`, detects unchanged OBS frames and
  uses the same tracking planner and continuous camera session.
- The repository's .NET 9 Windows tests could not be run because this macOS
  review environment does not have `dotnet`. A Windows Release build, regression
  suite and paired physical tracking test remain required before claiming
  smoother movement.

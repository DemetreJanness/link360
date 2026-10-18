# Community live tracking review — 2026-10-18

## Candidate and isolation

Target: Windows community build at 86f1dc8 (friend change 6c1a484 cherry-picked as 148abb0, plus the reviewed shared-frame age fix), with the mode-specific test instrumentation described below. No change was merged into local development or pushed remotely.

Original development remains at 1cc6b3c. Its executable, DLL and settings SHA256 values were checked before and after the test. The original ten room points were not edited. Community uses its own AppData profile and diagnostics directory.

## Test-button correction

Static inspection found that Test tracking always called TrackRoom, even with Link-only selected. In the community checkout it now selects the actual Track loop for Link-only, initializes that loop's origin/planner, and records optional local video/numeric diagnostics for the same 60-second budget. Room tests retain the deliberate Acer-only window at 35–40 seconds. Link-only logs identify their mode and have no forced-Acer window. Native manual mode cannot start a software test. Link-only does not require a room map. No planner gains, motor limits, thresholds or servo behavior were changed for these runs.

The first instrumentation fixture exposed test-reader file-sharing races and a cancellation-report edge case (a cancellation between iterations was labelled interrupted). These were fixed; the final simulated-worker suite has 32 passing checks. Default regressions also passed with the instrumented build. Hardware findings below are separate evidence.

## Procedure and local artifacts

Verified the native tracking flag was false through the existing read-only worker. The vendor app was closed. Closed the development UI normally, then opened the visibly labelled Community review build. OBS and Discord remained open. Acer used direct capture; the Link diagnostic images identify OBS receipt timestamps. Those ages omit upstream exposure/capture/OBS delay and are not end-to-end latency.

User authorized local test recordings previously and participated in both runs. Each run began seated/still, followed by slow/faster movement. Both cameras could still see the subject during forced Acer takeover. Cues are approximate; in particular the first recording shows standing/moving after 35 seconds, so that whole interval must not be treated as stationary.

Artifacts remain only in `%LOCALAPPDATA%/LINK360-Community/diagnostics/`:

- Link-only: `room-test-20261018-195340-bbb423e7` (.avi, .jsonl, .summary.json).
- Acer-assisted: `room-test-20261018-195540-7223266c` (same suffixes).
- Offline peripheral-flow/contact-sheet reviews: `link-only-review-20261018/` and `acer-review-20261018/`.

Commands: the isolated `LINK360.Tests.exe --review-paired-video <avi> <output-dir>` analyzed each saved video. Numeric summaries were derived from the JSONL sample records. No audio, uploads or runtime cloud service was used. No frames, personal map coordinates or capture files are committed in this report.

## Observations

### Link-only

Stopped at 55.857 seconds on the existing travel-limit guard. Logged 320 movement requests; these are requested targets, not 320 distinct optical motions. There were no stale/missing-Link samples under the receipt-age metric. It initially centred and had no movement requests from about 7.56 to 15 seconds. Moving/standing later produced repeated corrections and direction changes; the final stationary criterion cannot be scored using that entire interval because the subject was moving in the recorded frames. The travel-limit failure is sufficient to reject the overall run as a pass.

### Acer-assisted

Completed 60.027 seconds with 232 movement requests. Initial centring ended around 2.67 seconds, with no further requests before the movement cue at 15 seconds. After final settling at 45.214 seconds, there were no further movement requests through the end; subsequent driver snapshots remained pan -4, tilt 2. The video review shows the final view settled. This supports a bounded stillness pass for that final interval, not a general no-hunting guarantee.

During forced takeover, 7 movement requests were issued at 36.36–36.66 seconds; readback changed from (3,1) toward (7,6). The other recorded takeover samples were 19 outside-map holds and 2 room-wait outcomes. Most of the seated target positions fell below the covered part of the preserved room-point mesh. This exercises physical map-guided movement but does not establish reliable full recovery. The Link view was deliberately ignored during this window, rather than naturally losing the subject.

The user reported: “Acer-assisted felt better.” The two movement paths were not identical; image-flow figures are not a controlled speed comparison. Both moving intervals still contain abrupt background-motion increments. No smooth-cameraman-quality claim is supported.

## Placement change after testing

The user subsequently reported moving camera mounts and clarified this happened **after** both tests. Thus the recordings describe the former placement; the preserved map is invalid for the new placement. Used the community UI's Mounts moved action to mark CameraPlacementChanged while retaining all ten point values. No scan or new point collection was started. The original development profile remains unchanged and must not be used for room recovery with the new mount positions.

## Conclusions and next work

- Keep the friend candidate isolated; no acceptance/merge based on these runs.
- Link-only needs investigation of repeated corrections, anchor changes, travel bounds and delayed OBS image response. The shared-worker efficiency change alone is not a demonstrated smoothness fix.
- Acer-assisted held still after settling and was preferred by the user, but recovery coverage remains incomplete.
- Re-establish calibration for the new mount arrangement before further Acer recovery tests; retain the original map as historical evidence. Do not synthesize/extrapolate points to disguise missing calibration.
- Automated checks validate mode selection/logging support and shared-loop behavior; they do not validate optical motion, camera concurrency or smoothness.
Final full build/default regressions and all 32 shared-pipeline/diagnostic checks passed in `artifacts/review-lab/20261018-130040-b8384b5f`. Build: zero warnings/errors. The final report-cancellation fix was verified with simulated workers; it does not change the recorded motor behavior.


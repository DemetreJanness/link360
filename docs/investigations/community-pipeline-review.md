# Community review: shared Link tracking pipeline · 2026-10-18

## Target and scope

Reviewed `origin/codex/tracking-pipeline-efficiency` at
`6c1a484a2bdaecb022aca7da1c0d7b1b27d26e13` (one incoming commit). Imported with
`git cherry-pick -x` as `148abb0` into `codex/community-build` only, preserving
its separate `CommunityProfile`. Local development remains at `1cc6b3c`.

Incoming changes affect `ControllerForm.Track` and `WatchLinkPreview`, plus an
investigation note. No dependency, model, network endpoint, calibration,
command-unit, movement-limit or native-AI activation changes were included.
Source inspection was sufficient for this review; no REA runtime/tool readiness
or external protocol validation is claimed.

## Findings in the incoming commit

1. **Repeated observations bypass the age guard.** The shared path skipped an
   already-consumed timestamp before testing the 600 ms age limit. If preview
   delivery stalled, the application continued reporting a detected target and
   did not explicitly send the new stale-frame hold/reset. Reproduced against
   the actual `Track` method with a simulated camera worker. The existing native
   worker has its own 350 ms command watchdog, so this finding does not mean
   unlimited motor travel.
2. **Future timestamps poison the shared-observation cursor.** The incoming
   code advanced `lastSharedObservation` before rejecting a future timestamp.
   Subsequent correctly timed frames were then treated as duplicates. A frame
   dated one minute ahead reproduced failure to resume on valid frames.

The community-only follow-up checks freshness before duplicate suppression,
advances the cursor only for valid observations, and waits on the frame signal
while stale so the UI loop does not spin. Thresholds and motor rules are unchanged.
The author's incoming commit remains separate from this correction and tests.

## Validation and evidence

- Incoming commit: Windows Release build passed, zero warnings/errors; existing
  default regression suite passed. That suite did not cover the two findings.
- `tests/SharedPipelineTests.cs` invokes the actual private tracking loop on a
  hidden WinForms message thread. The form is never shown, so application
  startup, camera inventory, OBS monitoring and hotkeys do not run.
- The test executable intercepts its child camera and inference protocols. For
  fallback coverage, a synthetic capture process is injected before device
  discovery and OBS fallback is disabled. No camera images, GPU inference,
  physical PTZ or network service is involved in these fixtures.
- Before correction: 2 of 18 targeted checks failed (aged duplicate hold and
  recovery after a future timestamp). Local evidence:
  `artifacts/review-lab/shared-pipeline-original/results.json`.
- After correction: full existing regressions pass, plus 25 targeted checks for
  fresh shared observations, duplicate suppression, missing/stale/future input,
  recovery, no redundant shared-path inference worker, preview-failure fallback,
  disposal on return to sharing and process cancellation. Logs/results remain
  under ignored `artifacts/review-lab/`.
- The fixture command is included in `scripts/test-review-lab.ps1` and can also
  be run with `LINK360.Tests.exe --shared-pipeline-test <new-output-directory>`.
- Original and community settings still contain the same ten points. Their
  file hashes were checked before and after; no calibration was rewritten.

## Effect and remaining limits

The worker-saving change targets **Link-only mode with previews running**.
Acer-assisted `TrackRoom` already used the preview's observations, so it does
not gain this worker reduction. Its only incoming functional change is the
shared preview's frozen-OBS-image rejection.

The frozen-OBS branch was inspected but not tested against a live OBS server.
GPU/CPU savings, optical latency, movement smoothness and real camera handoffs
have not been measured for this candidate. The standard suite's camera and
reconnect cases are simulations. A physical A/B session remains necessary;
run one controller at a time, with built-in camera AI off and preserved points.
Do not describe this build as a verified fix for Acer recovery jerkiness.

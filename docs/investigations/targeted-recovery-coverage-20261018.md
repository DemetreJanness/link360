# Targeted Acer recovery coverage — 2026-10-18

Target: LINK360 community branch, based on 28d3f22. Question: extend the current
four-point map where the last live test reported outside-map positions, then
verify Acer correction without changing smoothing or local development.

## Evidence and implementation

The previous 60-second local test had 39 outside-map samples. All 22 samples in
its forced Acer window were outside the measured hull. That run established no
Acer correction; the user subsequently described the overall movement as smooth
enough. The task therefore concerns measured coverage, not motion tuning.

`ControllerForm.StartCoverageCapture` now invokes the existing stillness and
bounded subject-centering routine for exactly one additional point. Unlike
starting a new scene scan or person map, this explicitly extends a confirmed
unchanged placement. It checks native tracking off before capture and again
before saving, independently centres the Link using live subject detections,
and reads the camera pose. Existing scene points remain intact.

`RecoveryMapExtension` validates the original map and proposed expanded map,
rejects invalid/near-duplicate/extreme captures and the 25-point limit, and saves
an appended pair as RoomMapVersion 3 (scene map with measured subject extensions).
Placement identity, SceneHome and PreviousRoomPoints remain unchanged. A failed
save rolls back the active array and version. The current map is archived locally
before an extension attempt. A new empty-room scan still starts entirely fresh;
this extension action does not alter that contract.

The UI adds “Add recovery point” beneath the main preview. Capture requires
live previews, the built-in-tracking-off confirmation and unchanged mount
confirmation. It stops after one successful point and plays existing local
voice assets. No telemetry, external service, uploaded frame or new audio
recording is involved.

## Validation

`scripts/test-review-lab.ps1` built with zero warnings/errors and passed the
default fixtures and actual shared tracking-loop simulations. New fixtures
verify expansion into a previously uncovered position, exact preservation of
prior pairs and the old ten-point array, no extrapolation, duplicate/invalid/
extreme/capacity rejection, placement and incomplete-scan gates, save rollback
and persistence/reload. Report: `artifacts/review-lab/20261018-141001-a90ecb52`.

Hashes of LocalTracking.cs, HybridTrackingPlanner.cs and ContinuousMotion.cs
remain equal to the start-of-task hashes. Primary settings and its old ten-point
archive also match their established baseline hashes. No smoothing source was
edited.

Initial runtime previews failed for Link while Acer opened. A one-frame direct
Link probe subsequently succeeded (numeric status only; no saved image), then
both previews recovered on restart. The Link image appeared tilted/upward.
Physical coverage capture is waiting on the user's mount-placement/readiness
reply; no new point or physical recovery result is claimed yet. The four current
points and ten preserved previous points remain in the community profile.

## Live capture fixes and test-isolation incident

After mount placement was confirmed unchanged, live capture reached centering
but the new final native-state query incorrectly used a continuous camera
session. That session intentionally rejects extension requests. The query now
uses a separate worker. A new integration fixture exercises the actual
CentreAndRecord method with synthetic image, body and camera workers, including
that separate-worker boundary, real persistence and prior-pair preservation.

The first successful fixture run exposed a test-isolation error: a hidden form
used the default real community settings path. Synthetic calibration fields were
written to the community profile. No physical point was accepted from that
profile. The app was closed and the four real pairs restored from two agreeing
records: the successful local scene report and its pre-extension map archive.
The previous ten-point field was restored from its unchanged original archive.
Primary settings and original archive hashes still matched baseline. The
incident profile was preserved locally for diagnosis; it is not committed.

ControllerForm now accepts an explicit isolated settings path, and every hidden
form fixture supplies its own fixture-directory path. The corrected test run
passed; the restored real profile's hash was unchanged before/after that run.
The review runner additionally rejects any live community profile hash change.

Link startup also repeatedly lost its first capture while parked. DirectFrames
now allows 20 seconds until the first delivered frame, retaining the existing
seven-second read deadline thereafter. An eight-second synthetic startup test
passes, normal subsequent delivery remains fast, and cancellation releases the
worker. This change affects startup allowance, not tracking smoothing or its
freshness guards.

A subsequent real capture saved one additional subject-centred pair. The map
contains five real points and preserves the original four and prior ten-point
field. This first pair falls inside existing coverage; an outer TV-side capture
and another physical recovery test are still in progress. No expanded-coverage
or recovery-success claim is made from the interior point alone.

## Six-point physical coverage check

A standing TV-side capture saved the sixth real pair at Acer (0.065354,
0.517664), Link pan -32, tilt 10. This extends the measured hull; point five
was an interior refinement. All original four pairs and PreviousRoomPoints
remain preserved. The user confirmed unchanged mounts.

The 60-second local numeric test `room-test-20261018-213144-2583083b`
completed with both feeds fresh: 539 samples, no missing/ambiguous room target,
no stale room or Link samples. The forced Acer interval produced 49 movement
requests across 61 samples, with seven outside-map holds and five room waits.
This exercises actual Acer correction, unlike the previous all-outside-map
forced interval. The user judged the handoff/settling “Smooth enough.”

There were 28 outside-map samples overall, so coverage remains incomplete.
The first 15 seconds included initial reacquisition from the outer capture
pose; the last ten seconds still showed a large observed target span and 75
movement requests. This run does not establish zero stationary hunting, nor
complete room coverage. No video was recorded for this run.

The guarded review runner passed after closing the community app:
`artifacts/review-lab/20261018-143336-1e725c39`. Build: zero warnings/errors;
default fixtures and shared-loop integrations passed; live community profile
hash remained unchanged throughout the run. The direct-startup and native-query
fixes are included, with tracking smoothing sources unchanged.

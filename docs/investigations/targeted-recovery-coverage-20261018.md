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

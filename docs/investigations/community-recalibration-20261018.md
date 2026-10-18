# Community recalibration after camera mount movement — 2026-10-18

## Target and question

Community build based on d4b4f45, isolated from codex/local-development at
1cc6b3c. The user moved camera mounts after the earlier comparison runs and
requested new calibration, a verbal leave-room countdown, and an Acer recovery
retest. Preserve the original ten points and primary build.

## Evidence and observed result

- Made a byte-identical community profile backup in the local Community
  preserved-maps directory before testing. The primary profile still contains
  its original ten points. Both profiles and primary exe/dll hashes match the
  previously recorded baseline at the end of this attempt.
- Confirmed built-in tracking was off with the existing read-only camera
  adapter. Temporarily released OBS's verified Link source and Virtual Camera
  so both scan feeds used direct camera capture. Neither streaming nor recording
  was active. Restored the Link source and verified Virtual Camera active after
  the attempt. Discord's received image was not independently checked here.
- Ran the existing 30-second countdown and local WAV announcement path. The
  dialog progressed through the countdown; no audio exception was reported.
  Audible playback was not independently verified by the agent.
- Local report: scene-scan-20261018-200520-da4f983a0b1d406882dded3651000940.json
  in the Community diagnostics directory, outside OneDrive. No frames or
  private settings are added to Git.
- Scan verified one centre anchor, then stopped at the upper-left search.
  Final reason: fixed features did not surround the Link centre; homography
  would extrapolate (9% outside support in the last SIFT result). Earlier ORB
  results in that held pose were also outside their feature hull.
- No completed SceneReference was saved. CameraPlacementChanged remains true;
  the old map is blocked for current-placement recovery. Ten old points remain
  unchanged in both the active community profile and the preservation copy.

## Interpretation and implementation

The current room does not provide reliable feature support for this automatic
five-anchor attempt. High inlier ratios alone do not justify extrapolation.
The failure does not prove that a person-based recovery map cannot work.
Do not weaken feature-hull acceptance or invent unmeasured boundary anchors.

Added an explicit Guided calibration alternative in the community build only:

- GuidedPlacementId identifies a new person-calibration placement separately
  from SceneHome. A guided placement never claims an empty-room scan succeeded.
- RequireScene still rejects moved or unconfirmed mounts, conflicting/invalid
  reference identities, and recovery maps belonging to another placement.
- New collection archives the previous map and retains PreviousRoomPoints,
  then starts an empty active map tied to the new placement. Failed persistence
  restores the in-memory map. Opening calibration displays the active new map
  instead of offering the previous map's ready state.
- Starting another guided placement closes an existing calibration dialog.
  Successful automatic setup clears the guided identity. Existing five-anchor
  validation and person-map fitting/coverage limits remain unchanged.
- Fresh maps still require at least four measured person points before fitting
  and recovery testing. No old points are transformed or mixed into the new map.

Contract impact: settings gains an optional GuidedPlacementId field. Community
RequireScene / PrepareSceneMap use the selected placement identity; all other
transport and tracking contracts remain unchanged. No runtime network service,
REA dependency, model download, or telemetry was added. Source and live local
reports were sufficient for this investigation; no new REA session was used.

## Validation and remaining work

Offline restore/build and the complete default regression suite passed, followed
by all shared-pipeline diagnostics. Build: zero warnings and errors. New checks
cover guided old-map rejection, mount movement, explicit confirmation,
conflicting/blank references, preservation of points in policy checks, and
settings serialization round trip. Existing incomplete-scene rejection still
passes. Report directory: artifacts/review-lab/20261018-130827-8afcd704.

Reopened the updated community app and visually verified the Guided calibration
button. It has not been activated: waiting for the user's choice and physical
participation in fresh point collection. No new map or new-placement Acer
recovery success is claimed. Next: collect spread left/right, near/far and
seated positions, inspect coverage, then run and review the 60-second hybrid
test with its forced Acer takeover at 35–40 seconds. Keep the old map archives
and primary build untouched.
## Requested automatic retry (20:14–20:15 UTC)

The user chose another empty-room scan, not the guided alternative. Guided
calibration was not activated. Read-only native tracking query returned false.
The leave-room countdown was set to 30 seconds. After the user started it in
the UI, observed both direct previews and the empty shared view during the
countdown. No movement was sent outside the existing bounded scan controller.

Local report: scene-scan-20261018-201548-2b888c5a6cf94e06a947fcb5b5cb7a0e.json.
The attempt finished normally as a failure (not cancellation), again verifying
only the centre. It advanced farther toward upper-left, then held through four
failed matching attempts (11, 14, 17, 15 matches, below the required 20).
No scene reference or replacement map was saved, and no Acer recovery test
was attempted using the stale ten-point map. Do not treat 15/20 as sufficient
by simply lowering the threshold: feature distribution and geometric support
also remain necessary.

Rechecked primary exe/dll/profile hashes and community profile/backup hashes:
all unchanged. Re-enabled the verified OBS Link source and restarted its
previously active Virtual Camera after releasing the scan feeds. Runtime
processing and diagnostic reports remained local. Current limitation is the
automatic scan's fixed-background support near the boundary, not a successful
new-placement calibration. Further automatic scanning needs a meaningful
change to scene evidence or the registration method; repeating unchanged
attempts is unlikely to resolve the observed limitation.
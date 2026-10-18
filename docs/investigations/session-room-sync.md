# Session room alignment and visible-subject recovery test · 2026-10-14

The user cannot reliably leave the Link field of view and proposes a shared
home/focal reference or sync button. Source inspection: saved RoomPoint values
already pair Acer image coordinates with absolute driver pan/tilt readings.
RoomMap interpolates within their convex hull. It is a measured image-to-PTZ
map, not hardware 3D triangulation. Driver range readings do not establish
relative camera positions, lens calibration, distance or target identity.
No vendor USB protocol or new hardware homing command is introduced.

## Sync subject

`RoomAlignment.cs` computes a small session-only pan/tilt bias between the
map prediction and the current camera reading while a torso is centred in Link.
Five distinct fresh observations must show one stable subject in each view;
Link torso error must be within .065 per axis. Acer position must stay within
.025 normalized units of its initial point. Newest frames must be under300ms,
the entire sample window under1200ms. Stale, repeated, ambiguous, off-centre,
outside-map and over-ten-unit corrections fail without replacing prior alignment.
Hardware range/quantization checks still apply to every aligned map target.

The button pauses tracking, reads native state (must be off), observes without
motor writes, rereads camera state and rejects a changed PTZ pose. Only a
successful alignment replaces the previous session bias. Neither the ten saved
points nor motor home is written. Preview restart/pause clears the alignment.
This can correct a small common aiming offset. It cannot repair moved cameras,
depth/parallax errors or a distorted map. More than ten units requires checking
placement/calibration instead of silently translating a bad map.

## Recovery test with both cameras seeing the person

Room-map tests temporarily ignore the Link target from35 to40seconds. Acer
must still have one fresh subject inside the calibrated area. Actual multiple
Link detections remain an ambiguity hold. Both previews remain visible and
recordable. After40seconds, the existing stable Link reacquisition policy resumes.
Stop/manual override works throughout. Normal tracking never forces this loss.

Numeric schema3 logs record the alignment and forced-test window; each sample
has ForcedAcer. Video/status label the override as FORCED ACER TEST. This proves
only the commanded handoff path and observed response with a visible subject;
it is not evidence that natural occlusion detection/recovery passed.

## Validation

Build/test suite including saved-ten-point reproduction passes. New fixtures
verify known alignment, unchanged map, stale/repeated/off-centre/excessive
correction rejection and camera-range bounds. No new physical movement has been
claimed verified. Live sync and forced-handoff video remain the next test.
REA is unnecessary for this source-level LINK360 feature; native tracking
investigation and its activation gate remain unchanged.

# Automatic empty-room scene setup

Target: LINK360 Windows prototype, original Insta360 Link and Acer FHD User Facing.
Date: October 14, 2026 (user's local date).

## User requirement and interpretation

The user chose an automatic empty-room scan over walking through five positions.
Setup must precede new person-map collection, with a spoken leave-room countdown
and completion/failure announcement. Camera mount changes require setup again.
Chairs and other objects may move. An object inventory should support future scene
direction and later custom labels/training.

The implementation seeks centre plus four inset boundary targets in the Acer
image: (.5,.5), (.15,.2), (.85,.2), (.85,.8), (.15,.8). These are image-space scene
anchors associated with verified driver pan/tilt/zoom readings. They are not room
corners in metres, a depth map, full motor-range calibration, or fabricated person
tracking points. Insufficient common view or texture returns an explicit failure.

October 17 update: reachable boundaries may fall inside the requested image
edges. Feature loss can trigger a controlled revisit of the last supported pose
while capture/control/native-off checks still pass. A candidate becomes an anchor
only after two fresh optical matches and stable background. Coverage must surround
the centre and span at least 20% of each image axis. Reports and UI explicitly
mark limited boundaries; absent edges are not fabricated. Each new quadrant starts
from the verified centre. See `scene-scan-startup.md` for the live evidence.

## Evidence and implementation

Inspected existing CameraFeeds, DirectFrames, CameraEngine, native selector read,
calibration state/persistence, and the pinned local YOLOX decoder before extending
them. Existing Windows camera control contracts are reused. REA/raw USB additions
were not needed to investigate these already available source contracts.

- `SceneRegistration.cs`: ORB descriptors with SIFT fallback, nearest-neighbour ratio filtering,
  unique destination matches, RANSAC homography, minimum inliers and spread,
  centre containment in the source inlier hull, and local scale/orientation checks.
- Masks exclude every detected object category (chairs included), expanded by 16
  pixels. People block registration. Detector misses remain possible; temporal
  background stability is also required before an anchor is saved.
- Scene registration is a planar approximation. Parallax, repeated wall patterns,
  exposure, low light and weak overlap can cause rejection or inaccurate matches.
  Passing synthetic fixtures does not establish alignment accuracy in this room.
- `SceneScanPolicy.cs`: fresh direct frames, driver bounds/steps, maximum correction
  four units, total travel envelope 60 pan/40 tilt units, five measured anchors and
  scene/map placement identity checks.
- `SceneScanController.cs`: verifies the existing native-tracking read reports off;
  reads it again after countdown; uses a four-minute scan deadline; stops after
  repeated lack of progress. No native tracking enable payload is sent.
- Successful scans return to the measured centre pose and verify its optical
  alignment again before saving. Failed/cancelled scans stop at the current pose;
  “Previous framing” remains an explicit manual action. No recovery movement is
  issued automatically after a scan error or disconnect.
- `SceneScanDialog.cs`: two views, target markers, sampled fixed-feature dots,
  object overlays, editable 10–120 second leave time (default 30), explicit stop,
  mount-change action, point-collection entry and local report.

## Local voice and inventory

Speech assets are generated offline with the existing pinned Piper/Cori model.
`scripts/generate-scene-audio.ps1` validates the voice model hash and updates audio
provenance. There is no runtime speech service. Countdown ticks take at least a
second; a longer spoken numeral can slightly extend the selected leave time.

Object labels follow the pinned [OpenCV Zoo YOLOX class order](https://github.com/opencv/opencv_zoo/blob/47534e27c9851bb1128ccc0102f1145e27f23f98/models/object_detection_yolox/demo.py).
The existing model can report COCO categories, including cup, chair and bottle.
These are per-view observations with scores and pixel boxes, not unique object
identities. Small or poorly lit objects may be missed. No image-training workflow
or custom object identification is implemented in this pass.

Reports live under `%LOCALAPPDATA%/LINK360/diagnostics/scene-scan-*.json`, bounded
to ten scan reports. They contain numeric alignment evidence and object metadata,
including failed scans; they contain no images or audio. Object inventory fields
are excluded from scene anchors during settings serialization, so those details
are not copied to the project's OneDrive-located settings. A regression check
verifies this exclusion and verifies inventory remains in the local report.
New person-map collection
archives the prior coordinates locally before starting a map tied to the new scene.
Old coordinates are not rescaled, overwritten with synthetic points, or mixed with
another camera placement. The current eight-point map was byte-for-byte unchanged
at installation (SHA256 88CFB125E8B0E5B5053D7B3AFC319AA767D1F7A122C3B24C1765D3CCDE91C8C3).

## Validation status

Release build and regression suite pass. Tests cover a known synthetic view
translation, person/featureless/masked-object rejection, moving-background
rejection, object masking during stability checks, frame age/timestamp rejection,
bounded motor targets, incomplete anchor rejection, placement/map gating and voice
asset availability. Existing tracking, saved-map reproduction and sharing/recovery
fixtures also pass; this is not a new live OBS/Discord concurrency test.

Rendered the actual scene controls with synthetic frames at 920×720 and 1120×780;
checked labels, buttons and overlays. No desktop screenshot or camera image was
used for those renders. The real physical empty-room scan is awaiting the user's
test after installation. That initial status is superseded by the October 17
investigation: one live adaptive run verified the centre and both upper boundaries,
then stopped on a possible person at the lower-right view. Five-anchor completion
and final optical home return remain unverified.

Mount movement cannot currently be inferred reliably from every camera change.
Users must select “Mounts moved”; a saved scene also requires confirmation that
mounts are unchanged after app restart. Normal PTZ rotation does not mean the
camera mount moved.

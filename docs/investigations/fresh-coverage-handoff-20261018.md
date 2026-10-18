# Fresh paired coverage and head-loss handoff

Date: 2026-10-18. Community build only. Original Link and fixed Acer, unchanged
native transport/velocity gains. Question: repair the left-side dropout and make
calibration usable without overwriting the saved six/archived ten-point maps.

## Observed failure and investigation

The previous higher-speed live log showed a visible person in Link with
Anchor=missing, followed by outside-map holds from Acer. Static trace:
Program.cs emits missing when neither the head nor shoulder anchor is reliable;
LinkLock treats it as no guidance; HybridTrackingPlanner then asks RoomMap for a
target inside its measured hull. This can abandon an otherwise visible body.
No additional proprietary protocol investigation/write is needed for this fix;
REA transport findings remain as recorded in the earlier native-speed records.

## Changed contracts

BodyReacquisition accepts a finite, confident, sufficiently large single-person
box only when Acer also has one fresh unambiguous subject. It supplies horizontal
image acquisition only; tilt is held until a reliable head/shoulder lock returns.
It is not promoted to anatomical lock, identity proof or a calibration reference.
Normal stale/duplicate/multiple-person guards, driver bounds, leases and existing
speed curves remain. A usable visible body can therefore acquire left/right even
when the room map does not cover that image position. When Link has no usable
person, actual Acer recovery still requires a valid map and measured coverage.

FreshCoverageSession starts empty, gets a unique generation ID, and never reads
or mutates Settings.RoomPoints/PreviousRoomPoints. It collects actual paired Acer
coordinates and measured Link pan/tilt while the Link head/shoulders are framed,
motors report zero, pose is stable, and both frame streams are fresh. Collection
requires at least six unique paired observations over 1.2s; this exceeds the
observed coarse angle reporting interval. A 4%-image separation avoids duplicates.
Minimum four points with left/right AND near/far spread must pass the existing
mesh validation; at most 25 real points. No synthetic points or unbounded map
extrapolation are added. The current two-view setup is not a 3D room model.

Each debug map may be used for one test only, then is frozen/consumed. A new test
requires a new calibration generation. Numeric snapshots are saved only in local
AppData diagnostics as fresh-coverage-<generation>.local.json. Persistent presets,
scene setup, saved maps and primary development stay untouched.

CoverageWizard shares the already-running camera frames/detectors: two live
views, coloured capture feedback, coverage hull/gap suggestion, capture-progress
bar, explanation of why capture is held, and start/test/stop actions. Fixed British
voice instructions and capture confirmations are local assets. A gap ring is an
image-space suggestion within measured coverage, not a detected walkable location.

Room test schema 6 adds freshCoverageId and explicit Guidance text. Fresh-map
runs now force Acer at 23–28s on the left and 40–45s on the right, after local voice cues. The physical run below used the earlier 18–23s / 35–40s schedule; the final cue-spacing followup has software coverage only. The map is frozen
throughout the run; normal legacy runs retain the previous takeover window.
Test poses, frame-age limitations and measurements retain the prior qualifications.

## Checks and initial live calibration

Guarded build/tests: 20261018-155204-ba887870 passed, zero warnings/errors.
New fixtures cover empty/new-generation state, actual paired capture, four-point
coverage, frozen consumed map, moving/stale/duplicate/uncertain rejection, left
and right visible-body reacquisition, ambiguity hold and Acer recovery using only
fresh points. Existing controller/shared-pipeline checks also passed.

User was ready for guided calibration. Both direct previews opened. Actual runtime
captured four fresh points automatically and passed the spread checks, then nine
points as the user moved around, including image x approximately 0.12–0.97 and
substantial vertical spread. Capture confirmations and green feedback were visible.
The private generation log is kept in AppData; no per-user point array or camera
frames is committed. The same untested generation was explicitly resumed at the user request to keep collecting, reaching 25 actual points before its sole test. Consumed maps cannot resume. No completed-map reuse was introduced.

## Limitations and required acceptance

A large person box in each view is not verified cross-view identity. Ambiguity
holds remain mandatory. Horizontal reacquisition cannot recover someone who is
not detected by Link and is outside measured Acer coverage. Fresh points collected
while cropped/seated/standing may still have posture/depth bias; empirical left /
right takeover and stillness are required. These observations do not establish
camera relocation detection, universal room coverage or a Gaussian reconstruction.

The primary build/settings and real community settings are compared against the
existing SHA256 baseline. No motor speed source changes belong to this task.

## Accepted physical run and final checks

Live test room-test-20261018-230209-53c41c3a completed in 60,076 ms using the
25-point fresh map, frozen throughout: 580 samples, zero outside-map holds,
zero missing/ambiguous Acer samples, one initial stale sample per stream.
Left takeover contained 99 samples and 82 nonzero velocity requests; Link
reacquired head framing after takeover. Right takeover contained 23 samples
with zero nonzero speeds because the mapped goal was already reached. This
verifies a quiet right handoff, not recovery after deliberate right displacement.
The user reported: "Left side works; handoff is acceptable".
Movement requests are issued controls, not physical jerk measurements. No test
video/audio was recorded. Posture changed late in the run, so this does not
establish zero stationary hunting or universally smooth room-wide tracking.

Final software followups stop collection automatically at 25 points, queue local
capture/completion voice, cancel asynchronous startup on Stop/close, show startup
and loop failures in the wizard, and reserve the freshness warning for repeated
held samples while retaining all raw counters. Same-generation resume is limited
to an explicit ID, unconsumed local snapshot, and a 30-minute age check.

Guarded final build/tests 20261018-160636-9e2bb613 passed with zero warnings/errors,
including fresh paired capture, consumed-map rejection, bilateral reacquisition,
Acer recovery, fresh/legacy takeover windows, cancellation and existing pipeline
fixtures. Native speed policy files were unchanged. SHA256 checks confirmed the
real community settings and primary settings/exe/dll are byte-identical to the
baseline. OBS Link input and Virtual Camera were restored and independently
reported active. Private calibration data remains in local AppData diagnostics.
Final independent native read returned success, built-in tracking off and pan/tilt speeds 0/0.

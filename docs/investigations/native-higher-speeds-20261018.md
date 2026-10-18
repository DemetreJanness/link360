# Higher native speeds and left-side tracking loss

Date: 2026-10-18. Community branch only. Target and firmware qualifications are
unchanged from native-speed-adapter-20261018.md. Question: establish the native
speed range, then test faster visual catch-up without rewriting calibration.

## Additional static evidence

Reused the recorded REA/Ghidra selector-22 encoding trace; no new live REA
connection is claimed. Focused static inspection found a Qt zlib-compressed
control-panel resource at vendor PE file offset 0x672e4d1. Local helper:
`.rea-tools/inspect-speed-qml.mjs` in the primary investigation workspace.
The final focused extraction was saved privately as
`.rea-tools/native-control-panel.private.qml` (not committed).
The joystick inner/outer regions assign signed inputs 0.5/1 respectively and
release assigns zero. Together with the observed native conversion
int(abs(input)*9+1), this establishes a static native UI magnitude ceiling of 10.
It is input-range evidence, not a measured motor speed or firmware guarantee.
Proprietary resource text is not reused in application source.

## Bounded hardware probe

`LINK360.Tests.exe --native-higher-speed-probe <community exe> <local report>`
uses the independent neutral supervisor, limited travel, pose scaling crosscheck,
short repeated demands, explicit holds, native readback and home restoration.
The saved report `artifacts/review-lab/native-higher-speed-physical.json` is private.
Each pulse targets 900ms including ramp; report elapsed time includes optical
analysis/readbacks, and is not a calibrated degrees/second measurement.

| Request | Maximum sent | Measured angle change | Neutral verified |
| --- | --- | --- | --- |
| Pan +4 | 4 | +8.0 degrees | yes |
| Pan +6 | 6 | +11.0 degrees | yes |
| Pan +10 | 10 | +14.1 degrees | yes |
| Pan -10 | 10 | -14.9 degrees | yes |
| Tilt +6 | 6 | +10.9 degrees | yes |
| Tilt +10 | 10 | +14.9 degrees | yes |
| Tilt -10 | 10 | -13.4 degrees | yes |

The nonrecursive worker-kill neutral test passed again. Before/after frames for
pan +10 were visually inspected and show real background motion. Automated
feature translation failed for most larger changes; tilt +6 had 21 inliers and
52.26px vertical translation. Do not describe all rows as optical-estimator passes.
Frames remain outside OneDrive under local AppData diagnostics.

Important observation: during sustained velocity, selector-2 angle values often
repeat for roughly 400–500ms and then jump. Repeated reads are not independent
fresh optical samples. This is consistent with coarse native telemetry; exact
firmware sampling/cache source is unproven. It limits fast absolute-goal Acer
servo behavior. A host read timestamp is not the angle's sensor timestamp.

## Implementation and live results

Encoder and slew accept the statically derived, physically exercised range 0–10.
Experimental visual tracking uses a catch-up curve with the previous low levels
near centre and higher levels for larger image error. Deadband, filtering,
60ms single-level slew, immediate neutral, stale-frame lease, and default-off
session-only opt-in remain. Acer mapped-goal demand remains capped at 4.
A regression exposed that an earlier high visual velocity could persist during
slew-down after entering Acer target mode; the final slew now caps its current
state immediately at the selected mode's maximum. A fixture covers 10-to-4
handoff. This final correction is unit/integration tested, not separately accepted
by a new human-motion run.

Four fixed British voice cue files were generated locally with the existing
checksum-verified Cori/Piper model. The test announces stillness, slow movement,
fast movement and settling. No inference data enters speech generation. Source
script, generated assets and their provenance are included for reproducibility.

Live run: room-test-20261018-224047-ceeec7c9, 60 seconds / 630 samples.
Initial 15 seconds: zero nonzero motor-speed samples, unchanged reported pan -4 /
tilt -1. Moving phase reached speed 10. Forced Acer takeover ran at 35–40s;
the pre-fix log includes previous visual speeds above the intended recovery cap.
The Link held pan -2 for approximately 40–54s. User confirmed they moved again
near the end, consistent with Acer x moving from ~0.505 to ~0.7; late corrections
are not evidence of stationary hunting. Full run had 41 outside-map samples,
no missing/ambiguous room subject, and one stale sample per feed. RMS image error
0.1875 is not a controlled A/B latency benchmark (motion differed between runs).

User reported failure to follow to the left. The log shows both commanded pan
signs and actual changes. At multiple direction changes the Link anchor becomes
missing, then Acer returns outside-map and commands a hold. This establishes a
coverage/lock failure in this run, not a motor unable to turn in one direction.
Do not claim the left-follow issue or full-speed smoothness is solved.

Guarded regression runs 20261018-153637-0c5c0905 and
20261018-153938-14915529 passed. A later rebuild failed because user activity left
the community app open and locked its executable; after fresh UI observation and
normal shutdown the build was retried. See final guarded report for handoff fix.
No camera disconnect, full-load latency or simultaneous native-AI speed ownership
test was performed. Native tracking activation payload gates remain unchanged.

## Mapping direction, separate from this adapter

User wants movement-based room learning and stable scene landmarks, using the
mounted Acer/Link only for now; a phone walkthrough and full-room Gaussian splat
are deferred. No point cloud or Gaussian model was generated in this work.
Two fixed viewpoints plus PTZ rotation do not provide broad translated-view room
coverage. A partial geometric model must be validated by cross-view feature
matches, camera calibration and reprojection error before it can guide a camera.
A rendered splat alone is not metric tracking ground truth.

The proposed replacement for fixed capture poses is passive paired observation:
when the Link has a reliable centred head/shoulder lock, associate measured PTZ
pose with the same subject in Acer, accounting for frame/pose delay and uncertainty.
Learn in a separate candidate/session dataset, validate held-out trajectories,
then explicitly accept a model. Do not fabricate extra measured points. Exclude
people, TV imagery and movable chairs/cups from permanent scene landmarks.
Retain a clear recovery state and coverage display; test left/right, depth changes,
occlusion, stillness and camera relocation. This is a design direction, not an
implemented feature or validated multi-view identity association.

Runtime and diagnostics stay local. Primary executable/DLL/settings and real
community settings are compared to the preserved SHA256 baseline at completion;
current six points and old archived ten points must remain unchanged.

Final guarded run `20261018-154408-34b4cc65` passed, zero build warnings/errors,
including the revised recovery cap fixture. After normal community shutdown,
no LINK360 workers remained. OBS camera source and Virtual Camera both reported
active in a separate verification call. An independent native speed read returned
PanSpeed=0, TiltSpeed=0, no error. All four protected SHA256 values matched the
pre-existing baseline, including real community settings. No saved point changed.

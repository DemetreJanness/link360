# OpenCV and seated tracking review · 2026-10-17

## Target and question

User supplied https://github.com/opencv/opencv as a potential LINK360 dependency.
Investigate its useful role in local tracking, and diagnose hunting in the new
seated recording without changing the ten measured room points.

## Source evidence

- `src/desktop/Link360.Desktop.csproj` already references OpenCvSharp4 and its
  Windows runtime 4.13.0.20260627, plus ONNX Runtime DirectML 1.24.4.
- `LocalTracking.cs` uses OpenCV FaceDetectorYN with a checksum-verified local
  YuNet model. Body/pose inference uses local ONNX models with DirectML.
- `tests/VideoMotionReview.cs` uses OpenCV feature detection, pyramidal
  Lucas–Kanade flow and robust affine fitting to measure peripheral image
  movement in the explicitly supplied local paired recording.
- OpenCV's [optical flow documentation](https://docs.opencv.org/4.13.0/d4/dee/tutorial_optical_flow.html)
  confirms these APIs track image features. Apparent motion may come from the
  camera or objects; it is not a motor encoder or a calibrated depth map.
- Upstream repository inspected read-only. No upstream source copied, package
  upgrade, CUDA build, new runtime network dependency or driver change.
  Source and local replay supply the evidence here; REA was not needed or used
  for this particular investigation.

## Recorded findings

Local diagnostics remain in the real user's AppData directory, outside OneDrive.
`room-test-20261017-164551-2f185ec8` completed 60 seconds: 544 samples,
350 movement requests, 194 holds, no stale/missing room samples. This is not a
strictly motionless subject: the user leaned, drank and turned their head.
Seventeen request-source changes and 18 pan / 22 tilt requested reversals
warrant investigation; they are not measured motor reversals.

The 25-second frame's pose model put shoulders around the forehead and hips
around the neck. That frame's pose confidence was low and rejected, but the
old torso fallback geometry could accept the same anatomical arrangement at
higher confidence. A regression fixture now rejects that geometry explicitly.

## Implementation

- Head framing requires exactly one confident face within the upper part of
  exactly one detected person. The aim is lower face / neck, not identity.
- Fine tracking prefers that geometric head anchor. A verified upper-body
  pose remains a fallback; the legacy torso-only pose fallback is excluded
  from this runtime path. Acer coordinates and saved room points are unchanged.
- A session-only handoff correction can preserve a framing established from
  five fresh, steady, centred observations. It does not rewrite measurements
  or establish camera extrinsics. Excessive offsets remain rejected.
- The tracking status has reserved height so changing text cannot repeatedly
  move the controls during a live test.

Offline replay of 240 quarter-second samples from the recording found one body
in every sample, a valid head anchor in 118, and no upper-body fallback in the
remaining 122. Mean head detection was 4.43 ms after initial samples. Side
profiles remain a limitation; these are replay results, not live latency or
anatomical ground truth. The full regression suite passes.

The next physical test must establish whether the new anchors reduce hunting
and whether forced Acer takeover preserves framing. Optical flow between neural
detections is a candidate for short gaps, but must be gated against drift,
occlusion and stale frames before it controls the camera. Adding simulated room
points cannot add measurements or correct inaccurate real calibration points.

## Live head-framing run and feedback delay

`room-test-20261017-165154-1dee25b1` completed 60.022 seconds with 801 samples,
656 movement requests and no missing/stale camera samples. Head anchor loss
was much less frequent, but the camera still oscillated. In the final segment
the room target was nearly fixed (Y approximately .52–.53), while Link tilt
cycled roughly -6 to +1. Peripheral flow confirmed repeated vertical image
motion: 16 reversals above the diagnostic threshold during 40–60 seconds.
The head anchor alone does not solve the control instability.

`scripts/fit-tracking-response.mjs <log> 43000 60000` regresses Link image Y
against earlier commanded tilt. Best candidate delays were 260–300 ms, with
approximately .021 normalized image displacement per driver unit. This is an
exploratory fit under near-stationary-subject assumptions, not a calibrated
exposure timestamp, a universal device latency or a guaranteed response time.

The existing fine controller repeatedly added error corrections to the latest
commanded position, even before their optical effect reached the detector.
Filtering the moving image residual also prolonged corrections near centre.
The next change bounds corrections using the previous 350 ms of issued motion
and stops a correction when the newest observation crosses centre. It retains
range/step/cancellation gates and the continuous servo. This must pass delayed
closed-loop fixtures and a fresh physical test before a benefit is claimed.

## Feedback-budget seated check

`room-test-20261017-165621-530932ee` completed 60.062 seconds with zero movement
commands, zero driver span and no stale camera samples. OpenCV peripheral-flow
review found no directional reversals or >3-pixel steps; phase P95 Link motion
was .020–.035 pixels per 100 ms. The subject was looking down at their phone.
This establishes a quiet hold for that posture, not smooth acquisition from
an off-centre position: the controller started with the subject already framed.
The forced Acer interval included outside-map holds, so it does not establish
successful repositioning from that seated location. Deliberate movement and
off-centre settling are the next physical checks.

All original ten map coordinates still equal the preserved archive exactly.
Full regression suite including saved-map reproduction and delayed closed-loop
fixtures passes. No native-on command or firmware/driver change was involved.

## Deliberate movement follow-up

`room-test-20261017-165922-2fde7937` completed 60.031 seconds. Initial vertical
error of approximately -.18 settled from tilt -5 to +2 by about two seconds,
with no requested reversals; the camera then held until deliberate movement.
The overall test had 451 logged samples, 254 movement requests (162 Link,
92 Acer), four guidance-source changes, and no stale camera frames. Requested
pan/tilt reversals include the user's deliberate changes of direction and are
not independently measured motor reversals.

The user reported **still jerky or hunting**. Standing close to the cameras
also put the head above their views, leaving body-based Acer guidance unable
to establish the requested head framing. Forced takeover at 35–40 seconds is
an artificial test, not proof of natural occlusion recovery. Smoothness remains
unaccepted. A cached driver snapshot also became old when the mapped target
already matched the issued pose; the controller now requests a read-only
snapshot after 150 ms in that branch. No movement command is added by that fix.

See `gpu-vision-benchmark.md` for the subsequent GPU/CPU comparison and the
installed faster head inference path. It preserves detection results; it is
not a claimed cure for the camera's physical movement behavior.

# GPU overview and body-based reacquisition

Date: October 13, 2026. Targets: original Insta360 Link, fixed Acer FHD User
Facing overview, OBS 32.1.0, controller 2.2.5.14. Question: improve slow/stepped
following and reacquire a person when the Link's built-in tracker loses them.

## Observed facts and limits

The user reported stuttering/stepped following even after the JPEG/short-motion
revision. Latest software updates measured 33–44ms without correction and 182ms
with correction. Native tracking, activated with a palm gesture while LINK360
software tracking was off and vendor app closed, was user-confirmed smoother.
The user reports native tracking gives up after loss; they want the fixed backup
camera to watch the room and use body detection. They confirmed fixed placement
and overlapping room views. The original Link quickstart describes built-in AI
and gesture activation. This supports using a camera-side tracking loop, but
does not recover its USB mode-control contract.

Installed graphics: Intel Iris Xe and NVIDIA RTX 3070 Ti Laptop GPU. The controller
installation contains DirectML, ONNX Runtime, MNN and TensorRT libraries. Static
presence does not establish which feature uses which backend. LINK360 now uses
the separately downloaded, pinned Microsoft ONNX Runtime DirectML 1.24.4. DXGI
enumeration explicitly selects the NVIDIA adapter; CPU is available for tests.

Local MediaPipe person inference detected one person on the Link source on both
CPU and GPU. Warm CPU runs measured 34/38ms; warm NVIDIA DirectML runs 2/4ms.
On the Acer, repeated samples also agreed on one person: CPU 28–46ms, GPU 2–3ms.
These are inference timings, not optical follow latency. Tests process source
images in memory; they save no frames or identity embeddings. Model commit,
checksum and license are in `models/body-provenance.json` and sibling license;
ONNX Runtime license is under `docs/licenses/`.
The worker sets `ORT_DISABLE_TELEMETRY=1` before runtime use and explicitly calls
`OrtEnv.DisableTelemetryEvents()` before any inference session. Failure of that
call prevents inference startup. Session profiling is not enabled.

OBS reports the Acer source is not active/showing in a scene. This initially
suggested a retained frame, but source inspection showed hidden capture can
continue. A read-only freshness test found two distinct frame hashes over four
samples while hidden; this confirms changing data in that run. No capture or
scene settings were changed. Runtime verifies the exact `Backup - Acer Camera`
source, dshow kind and `Acer FHD User Facing` device label, retaining the resolved
device path only in memory to reject a changed source.

## Implementation

`BodyTracking.cs` preprocesses 224x224 inputs and decodes MediaPipe person model
body-center virtual keypoints, with confidence filtering and overlap suppression.
This is a person/body detector, not a full skeletal pose estimator or identity
recognizer. Worker lifetime and GPU data stay local. `RoomTracking.cs` provides
overview frames, a cancellable GPU worker and a calibrated affine room map.

Room watch is explicitly enabled in the panel. At least four spatially spread
paired overview-body/Link-pose points are required. Fit rejects degenerate or
inconsistent points. Room mode never extrapolates outside the calibrated bounds,
checks hardware ranges and caps each correction to five units. Two stable body
observations are required. Missing, ambiguous or stale body data holds motion
without disabling room tracking. A returning stable body inside the mapped area
can resume movement. Manual controls stop tracking; room watch is separately
visible and switchable. Closing LINK360 terminates the workers.

Built-in native tracking must be disabled before calibration or room control;
the panel requires the user's explicit assertion. Its mode read/toggle has not
been recovered, so the software cannot independently guarantee that state. This
is a current integration limitation rather than a verified native hotkey.

## REA and protocol evidence

REA 6.4.0 local audit recognized Ghidra installation/runtime, but the current
production MCP doctor initialized/catalogued tools and then failed server
identity/request-flow checks at its deadline. Do not describe that current MCP
connection as ready. Earlier REA resource inspection remains valid evidence.
Static controller strings include tracking-mode and tracking-speed methods.

The independent fmontes/linkcam source was read as a reference; it identifies
Link 2 PID 4C04, unlike this original Link PID 4C01. Its extension selectors and
payloads have not been sent to the original camera. No firmware-specific writes,
driver replacement or speculative raw USB commands were performed.

## Hands-free calibration revision

The user cannot operate the keyboard at room positions and requested an arms-out
trigger. A pinned MediaPipe pose model now runs locally with DirectML, reading
shoulder/elbow/wrist landmarks. Only the arms-out boolean and timing leave its
worker; joint coordinates/masks are not stored or exposed. Required joints need
visibility/presence >=0.55, pose confidence >=0.5, extended wrists/elbows aligned
with the shoulder line, and a stable two-second hold. Arms must lower for 800ms
to rearm; nearby saved positions are rejected to prevent duplicate points.

The start button enables bounded automatic Link centering, then pairs its pose
with the overview body position and plays a local saved-point cue. Face centering
supports close-up views; torso centering is a fallback. Multiple/lost subjects,
movement during the hold, stale data, unavailable controls, a 20s deadline and
30-pan/20-tilt session travel limits reject the point. Fresh Link frame evidence
and three centered observations are required. Hidden Link source snapshots are
allowed only for this explicitly enabled calibration, without changing OBS scenes.
After four valid, spread-out points, a successful map fit ends calibration and
selects room/body mode; following still requires the toggle hotkey.

Pose inference tested its real output contract on both CPU and NVIDIA DirectML.
At resting arms, both returned false. Warm CPU pose inference was 34–43ms;
NVIDIA pose inference 3–5ms, plus body inference 2–3ms. Unit fixtures cover T-pose,
lowered arm, occluded wrist, low confidence, hold duration and release-to-rearm.
Actual arms-out recognition, automatic centering and point capture still require
the user's physical test before declaring hands-free mapping verified.

The first user gesture test was not confirmed in two sampled monitoring windows;
those windows had a gap, so this does not establish a gesture rejection. Repeated
room samples also returned no MediaPipe person, preventing pose inference. A
general YOLOX person-only detector now fronts the overview; its body boxes are
used to form the pose ROI, and other object classes are discarded. In the tested
sample it found one person on both CPU and GPU. Warm CPU inference was 862–899ms,
NVIDIA DirectML 13ms plus pose 4–6ms. This heavier model is appropriate for GPU
overview use; the small MediaPipe detector remains available for Link centering.
Pose diagnostics and an in-app positive-sample counter now retain recognition
evidence between assistant snapshots. No coordinates or image history are saved.

The installed build passed the motion, OBS recovery, face planner, room-map and
gesture fixture checks. A fresh local benchmark returned one person in all six
CPU/GPU samples, four distinct overview frames, and OBS Virtual Camera active.
Warm NVIDIA whole-person inference was 10–14ms plus pose 3–4ms; CPU was
672–715ms plus pose 26–27ms. This sampled resting pose reported low confidence
and no arms-out; these timings do not verify gesture recognition or motor motion.
Calibration remained off while requesting a repeat full-arm visibility test.

The user completed the repeat arms-out test. The running GUI retained 237
positive arms-out samples; the subsequent resting sample reported low pose
confidence, one body, NVIDIA DirectML 11ms plus pose 3ms, and zero map points.
This verifies real gesture recognition in the overview, not automatic centering
or point capture. Local tracking remained off throughout this recognition test.

The subsequent user report of no beeps was traced to the calibration start
prerequisite: GUI status showed the start rejected, and a screenshot confirmed
the native-tracking-off assertion checkbox remained unchecked. Overview retained
843 positive arms-out samples and zero points. No calibration motion or point
capture was verified; this result is not evidence of a failed centering loop.

## Validation still required (hardware)

GPU provider and source detections are runtime-verified. Mapping fit/range tests
cover representative and invalid values. Physical room calibration, torso
tracking with face turned away, lost-target reentry, multiple people, and manual
override during room-guided movement remain required before declaring complete
two-camera reacquisition. An anonymous single-body policy cannot promise the same
person after a long absence or distinguish them from another sole entrant.

Primary sources: [original Link manual](https://res.insta360.com/static/infr_base/6d7757c40569d7152f2e0795c11fb869/insta360LinkQT.pdf),
[MediaPipe model wrapper](https://github.com/opencv/opencv_zoo/tree/main/models/person_detection_mediapipe),
[ONNX Runtime DirectML](https://onnxruntime.ai/docs/execution-providers/DirectML-ExecutionProvider.html),
[OBS dshow source](https://github.com/obsproject/obs-studio/blob/32.1.0/plugins/win-dshow/win-dshow.cpp).

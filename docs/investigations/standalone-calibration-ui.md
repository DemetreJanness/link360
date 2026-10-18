# Standalone camera previews and calibration workspace

Target: LINK360 Windows/.NET 9 prototype; original Insta360 Link and Acer FHD
User Facing camera. User wants a tracking-first camera suite, explicit calibration
feedback, local torso tracking, no OBS requirement, and British female prompts.

## Evidence and implementation

- Inspected ControllerForm, RoomTracking, LocalTracking, native CameraProbe and
  OBS frame routing. Existing previews depended on local OBS screenshots.
- Prior arms-out attempts registered hundreds of positive samples, but capture
  was not armed because the native-tracking assertion was unchecked. Generic
  readiness errors and transient failure text made that hard to understand.
- Added named-device DirectShow enumeration and isolated OpenCV capture workers.
  Each role requires exactly one matching physical camera. Moniker identity is
  checked before/after opening; no guessed control packets or camera indices are
  persisted. Frames are JPEGs in process memory, never recorded or uploaded.
- Parent deadlines kill blocked capture workers. Direct capture is preferred;
  optional OBS fallback preserves an existing shared route when hardware is busy.
  Pausing previews cancels tracking and releases owned capture workers.
- Both direct worker smoke tests returned advancing JPEG sequences with OBS
  closed: Acer 1/8/14, Link 3/10/16. Commands:
  `node scripts/check-direct-preview.mjs .rea-tools/ui-build/LINK360.exe`.
- A later GUI attempt could not open Link. Runtime inspection found the vendor
  controller running again in the background. Closing the exact user-specified
  executable and restarting previews produced two GPU body workers and both
  direct camera workers. Acer GUI reported one torso, 90% confidence, 13–15ms
  inference. This demonstrates direct capture, not calibrated optical accuracy.
- Calibration is modeless and separate from main controls. It shows paired live
  views, body rectangles/torso centers, framing tolerance guides, numbered saved
  points, stillness progress, retained last failure, start/stop/reset controls.
  Green indicates a usable unique body or centered Link; amber indicates framing
  adjustment; red/held text indicates no unique body. No fabricated skeleton.
- User simplified capture to standing still. Two seconds of fresh, unique body
  observations within a 2.5% position band arm one centering attempt. A new point
  requires moving at least 7% of normalized room coordinates. Loss resets hold.
  Arm visibility is no longer required. Link centering consistently uses torso
  position from the generic person box, not a face/body target switch.
- Existing mixed face/body map points are retained in PreviousRoomPoints when
  the first new torso map starts; they are not mixed with new torso observations.
- Map validation remains at least four spread-out points, no extrapolation and
  bounded driver movement. A saved point can still leave an invalid map; the
  dialog retains the fit reason and asks for another position.

## Local speech assets

Windows only had US David/Zira SAPI voices and US OneCore voices installed.
Downloaded pinned Piper 2023.11.14-2 and a pinned Cori high UK female model for
development-only generation. Model card states UK English female and a public
domain LibriVox dataset. Source, hashes and exact text are in assets/audio/.
Only three fixed WAV clips ship; Piper/model/network speech is not a runtime
dependency. Playback says "Captured tracking point", then requests the next
point, or says "Done, thank you" when a valid map completes. No completion prompt
is triggered merely by stopping or closing calibration.

Sources: [Piper pinned release](https://github.com/rhasspy/piper/releases/tag/2023.11.14-2),
[Cori model card](https://huggingface.co/rhasspy/piper-voices/raw/main/en/en_GB/cori/high/MODEL_CARD).

## Validation and limits

Build passed with zero warnings/errors. Existing motion/OBS/face/map fixtures
passed, plus stillness capture, new-position rearm, missing-body reset and torso
planner direction/zoom checks. Camera workers and OBS-free frames are observed
runtime facts. Full room calibration, physical centering accuracy, voice playback
at real capture/completion, and reacquisition remain user hardware tests.
Body position is an approximate torso point derived from a person rectangle;
this is not skeletal localization or subject identity. Multiple people hold.
Current camera roles are the verified Acer and original Link. General camera
configuration/PTZ adapters and per-subject IDs remain future implementation.

REA adds no evidence needed for this source-level UI/capture change. Earlier
vendor investigation and REA readiness limitations remain recorded separately;
REA is not an application dependency.

## Suite layout follow-up

The UI/UX agent implemented Tracking/Cameras/Controls/Settings/Sharing navigation
with separate preview cards. The user's screenshot exposed clipped action
buttons in the fixed-height hero and a clipped diagnostics footer. Replaced
those fixed rows with content-sized cards, removed the redundant hero heading,
and let diagnostics wrap to the available width. The normal build passed with
zero warnings/errors. At the default approximately 1240x920 window, visual
inspection with previews initially off confirmed full action labels and footer
text. The user then started both direct previews; numeric GUI status reported
one torso, 92% confidence, 14ms GPU inference. A smaller-window drag was interrupted
by user input; runtime verification at the minimum window size remains pending.

## Repeated centering-limit rejection

The user supplied a calibration failure screenshot, then reported saved points
despite repeated failures. Read-only native inspection found pan -22 (range
-145..145), tilt 6 (-90..100), zoom 100. Four torso-map points were retained;
their saved pan positions span -22..20, exceeding the previous per-attempt
30-unit pan budget. This supports a software travel-budget rejection, not a
physical end-stop at the sampled pose; the old shared error did not distinguish
both cases. Wider attempts now permit 60 pan / 40 tilt units from their starting
pose, preserving <=5-unit corrections, driver bounds, fixed zoom and cancellation.
An optical error progress check stops after six non-improving observations; a
30-second deadline remains. Driver end-stops and travel exhaustion have distinct
messages. Point failures stop listening and offer retry at the same position
instead of overwriting failure with Listening. Existing points are preserved.
Fixtures passed for the observed 42-unit room span, continued improvement,
stalled/wrong-way rejection and maximum travel. Physical retry remains pending;
no autonomous movement was made for this investigation.

## Room-map test and local diagnostic logging

Maps now require 4–10 points. At four valid points the operator may finish or
continue adding points; ten points finish automatically when the fit is valid.
The calibration dialog exposes Finish calibration and Test room map only after
a valid fit. Completion speaks the bundled completion prompt.

An explicit 60-second room-map test uses the existing bounded room-following
loop. It records JSONL numeric samples and a separate JSON summary under the
application's logs directory. Samples include elapsed time, room coordinates,
confidence, counts, observation ages, model time, requested/readback PTZ pose,
command duration and the most recent Link centering error with its observation
age. Movement requests are always logged; holds are sampled at most 5Hz. Reports
include missing/ambiguous/stale observations, out-of-map holds, target reacquisition
gaps, observed room-coordinate spans and fresh-Link RMS centering error.
No images, identities, embeddings, device paths, serials, credentials or remote
uploads are accepted by the logger. Runs are capped at 60 seconds and 2MB; only
the latest ten owned test runs are retained. Runtime logs are ignored by Git via
the existing bin rule. Last test report is viewable from the main workspace.

The test starts only with a valid map, native tracking asserted off, both preview
sessions running, live room observations and connected controls. Manual override,
cancel and device/failure handling stop motion and preserve the partial report.
Reports are observations, not certification of physical tracking accuracy;
recent Link error can predate a motion command, so observation age is included.

Fixtures verified numeric log events, missing-target count, interrupted summary,
minimum/maximum point constraints and read-after-finish. They exposed a log file
handle remaining open after Finish; Finish now closes it immediately. All
existing suites and the new fixtures pass. Representative physical walk-through
and lost-target recovery testing remain pending; no live test was silently run.

## Ten-point room recovery and native-mode investigation

User screenshots showed single-camera travel-limit stopping and affine room-fit
rejection after ten captures. Startup did not retain/select the room mode. A
valid saved map now selects the two-camera path at startup. Fresh, unique Link
body observations guide fine centering; missing/stale Link observations use a
stable Acer room target. Multiple people hold; manual overrides still stop.

Replaced the single affine plane with Delaunay triangles and barycentric local
interpolation of captured PTZ correspondences. Targets stay inside the captured
convex hull, not merely a bounding rectangle. Driver limits, <=5-unit corrections
and unchanged zoom remain. Duplicate conflicting positions, collinearity and
extreme individual control outliers are rejected. This is measured 2D mapping,
not reconstruction of 3D body position. The user's real ten-point map was
accepted and every captured pan/tilt position reproduced exactly by a fixture
using those saved settings (`--saved-map`). This is numerical validation; actual
lost-subject recovery remains a physical test.

The user additionally wants native AI with Acer recovery and automatic native
restart, plus software-only tracking. Palm activation is observed; original-Link
native state/toggle commands remain unverified. No Link2 selector guesses sent.
Wireshark and USBPcap installed; USBPcap service running. CLI tools launched an
elevated USBPcap enumeration console rather than returning capture interfaces
in the non-elevated caller. Console enumeration verified Link under USBPcap2,
alongside input/audio devices. No packets captured: capture must first restrict
to the Link address and exclude unrelated keyboard/audio/video payloads.
REA Ghidra 12.1.4 scoped engine readiness passed with workspace Java/Ghidra paths;
audit-wide readiness remains false due to missing other providers/registration.
No claim of a connected live REA MCP session. Native automatic restart is not
implemented or verified yet.

## Physical recovery confirmation

After the ten-point mesh/hybrid update, the user tested leaving the Link view
while remaining visible to Acer and confirmed: "Yes, Acer brings the Link back."
This verifies the observed software-mode physical reacquisition scenario. It
does not verify native AI restart, multiple-subject association, every room
position or recovery latency. The expected normal-build logs directory was
absent on the immediate follow-up; numeric timing evidence cannot be inferred
from this confirmation alone.

## Follow-up responsiveness and smoothness tuning

User confirmed recovery but requested smoother/faster movement. Found deliberate
100ms room-loop and 150ms Link-loop waits. Reduced both to 10ms; direct capture
requests now wait for a newer driver frame instead of re-running detection on a
duplicate JPEG. Each worker still keeps only its latest frame in memory.
Short <=150ms corrections now have nominal 25ms intermediate trajectory points;
presets retain nominal 50ms points. Driver quantization, trajectory rate bounds,
fresh starting-value reads and endpoint verification remain unchanged. The
tracking camera binding caches immutable control ranges per session and reuses
the already-verified endpoint values for its response, removing redundant reads.

`scripts/measure-tracking-command.mjs` measured three 3-unit pan corrections and
restored the starting pose, preserving zoom. Before: 128/110/108ms (mean115).
After: 117/117/116ms (mean117). These small samples show no command-roundtrip
speed improvement; expected gains are reduced analysis waits and finer motion
steps, not a claim that driver timing is faster. Both direct cameras returned
advancing frame sequences in the subsequent capture smoke test.

Added a 120ms handoff hold after a fresh Link detection briefly disappears. A
sustained loss uses the Acer map; an initially missing or stale Link uses it
immediately. This prevents a single intermittent detector miss from switching
between fine-centering and mapped targets. Fixtures verify fine motion adjacency,
transient-loss hold, sustained-loss recovery and existing ten-point reproduction.
All suites passed. Perceived smoothness and full-loop latency still require a
user test; absolute 1-unit driver steps remain a practical movement limit.

## Continuous controller and studio pass

Replaced finite tracking bursts with a persistent continuous target worker.
Nominal25ms command ticks retain velocity across target updates, with bounded
acceleration/speed and driver quantization. Cached actual-driver snapshots update
at100ms. A response acknowledges a target; it does not verify target completion.
Missing/ambiguous detections and unavailable map targets issue hold;350ms without
a target holds automatically. Manual/preset actions cancel the worker first.
Finite preset/calibration endpoint validation is unchanged.

`scripts/check-continuous-motion.mjs` physically tested a four-unit pan target
with vendor software closed and built-in tracking confirmed off. Driver samples
were[-7,-7,-5,-3,-3,...]; endpoint/hold stable, tilt/zoom unchanged, original pose
restored by a separately verified finite request. Tests also cover reversal
without snapping against velocity. Whole-room perceived smoothness is untested.

Maps now permit4–25 points. The user's ten points remain intact and reproduce
every recorded PTZ position in tests. Partial Link/head fragments fail a geometric
torso-lock guard; Acer guidance remains available. New wide/torso framing choices
apply fixed100/160 zoom with a smooth bounded transition. No adaptive framing
or subject identity matching is claimed.

UI/UX agent built Link Studio's dominant Link view, smaller Acer inspector and
separate manual/settings pages. Runtime verification found clipping when the
inspector scrolled; headline/actions were pinned above the scroll region and
diagnostics gained an explicit scroll extent. A follow-up removes selector
horizontal overflow. Main calibration labels/overlays wrap;4–25 point minimum
and amber partial-Link guidance are explicit. Final build has no warnings/errors.

Native USB investigation and the blocked activation experiment are documented
separately in[native-tracking-usb.md](native-tracking-usb.md). Automatic native
restart remains gated. User requested a break; checkpoint is in
[NEXT-SESSION.md](../NEXT-SESSION.md).

# Native direction/speed adapter — community experiment

Date: 2026-10-18. Target: original Insta360 Link VID 2E1A/PID 4C01,
controller 2.2.5.14; camera firmware 1.4.8_build2 is user-reported.
Question: can captured native velocity controls replace stop-at-waypoint
tracking while keeping the existing map and standard-control path?

## Evidence and implementation

Protocol evidence and REA/Ghidra/Wireshark commands are recorded in
[native-speed-transport-20261018.md](native-speed-transport-20261018.md).
No new speculative USB payloads or driver replacement were used.

`src/native/NativeSpeedMotion.cs` implements IKsControl extension selector 22,
four bytes `[pan direction, magnitude, tilt direction, magnitude]`. The signed
API follows Windows axes; positive tilt uses wire FF. Neutral is 00 01 00 01.
Only levels 0–4 are admitted; higher native levels remain untested. Startup
requires one exact original-Link identity, a unique responding extension node,
manual-capable driver ranges, native AI off and vendor controller closed.

Actual native pose comes from selector 2 (52 bytes, signed int32 offsets 38/42,
tenths of a degree). Physical crosschecks of standard +4 pan and +3 tilt
produced +40 and +30 raw units. Windows IAM values can retain the last absolute
target while native speed moves; consumers therefore use native measured pose
once this session-only option has been selected. Requested goals stay separate.

`NativeSpeedPlanner` supplies direct image-error velocities rather than repeated
finite relative goals. Existing subject/confidence/body-shape guards remain.
Two fresh frames are required; duplicates do not renew the 300ms demand lease.
Lost/invalid/stale frames stop. A 0.065/0.035 hysteretic deadband suppresses
small movement. Catch-up demand reaches level 4 at a large image displacement.
Transport ramps by one speed level per 60ms, crosses neutral before reversal,
and stops immediately on explicit hold. Acer still supplies absolute map goals,
which the speed worker follows from live feedback. No room point is synthesized,
rewritten or removed by this adapter.

The control worker has a separate neutral supervisor. It verifies neutral before
motion and expects heartbeats (450ms timeout). The motion loop refreshes feedback,
clips outward travel near driver bounds and refuses zoom/native-mode changes.
EOF, cancellation and exceptions request neutral. An unresponsive issuing worker
is killed nonrecursively so its supervisor can still stop. This is not a guarantee
against a blocked camera driver, physical disconnect or killing the entire process
tree. Those failure cases remain separate validation gates.

UI exposes `Native speed control · experimental`, default off and session-only.
Manual takeover awaits the previous tracking loop shutdown before standard writes.
Wire requests add paired NativePanSpeed/NativeTiltSpeed fields and ReadNativeSpeed;
only a supervised speed session accepts speed demands. Numeric room-test schema 5
adds actual sent speed levels. Repeated demand samples are not physical jerk counts.
The standard transport and the native AI activation gate are retained.

## Validation

Guarded community build/test runs `20261018-152237-5964f33c` and
`20261018-152437-f9ee704f` passed, with zero build warnings/errors. Tests cover
captured packets, invalid speeds, lease expiry, reversal/neutral, pose scaling,
stationary jitter, catch-up demand, duplicate/stale/lost frames, actual shared-loop
speed routing and cancellation, alongside the existing regression suite.

Private local physical report: `artifacts/review-lab/native-speed-physical-v2.json`.
Short bounded requests at pan level 1 and 4 measured raw changes +11 and +28;
optical background translations were -5.22 and -13.18 pixels. Tilt up/down level 4
measured +37/-37 raw; up had 47 optical inliers, down had no reliable optical
estimate in this run (the preceding run measured down optical translation).
Neutral readback passed after each pulse and after nonrecursive issuing-worker
termination. Pulse-loop target was 420ms; report elapsed times include reads and
image analysis and must not be converted into calibrated motor speeds.
Images remain only in the local AppData diagnostics directory, outside OneDrive.

Live test `room-test-20261018-222912-d9544b55` completed 60 seconds, 290 samples.
Initial correction moved tilt from -9 to -6; thereafter pan/tilt held at 3/-6.
Final 15 seconds: 70 samples, zero nonzero speed samples, unchanged measured pose.
There were no missing/ambiguous Acer subjects. One stale sample occurred per feed.
Acer coordinate span was only 0.0086 x / 0.0045 y, so this run establishes seated
hold only, not following smoothness or fast catch-up. Forced Acer takeover had
23 outside-map samples and correctly held; it does not establish recovery motion.
A further user movement run is pending. No live smoothness improvement is claimed.

## Preservation and remaining gates

Only the community checkout/build changed. The real community settings, primary
settings and primary executable/DLL are checked against a pre-test SHA256 snapshot.
Six current community points and the archived ten-point map are preserved. No test
frames, recordings, device serials, credentials or packet captures belong in Git.
Runtime processing and numeric reports are local; optional video remains opt-in.

Remaining: representative human motion and Acer recovery in covered room areas,
manual override on the real device, unplug/reconnect, contention/native-mode changes,
long-duration load, and hardware/driver stall behavior. Native firmware AI smoothness
is a separate behavior; this capped host-side velocity adapter does not reproduce
its internal algorithm or establish its higher speed range.

## Movement run and cleanup

Follow-up live run `room-test-20261018-223154-74c99763` completed: 468 samples,
Acer span 0.4155 x / 0.2035 y, no missing/ambiguous room subjects. During the
forced 35–40s Acer interval, 46 of 60 samples reported nonzero speeds and
measured pan ranged -15 to -2, establishing actual Acer-directed movement.
Twelve takeover samples were outside-map (34 over the full run), so coverage
still limits recovery. Fresh Link error RMS was 0.1713 normalized during this
substantial-motion run; this is not a controlled latency benchmark.

User feedback: "Smoother but still too slow." The native velocity path improves
the perceived stepping, but fast catch-up acceptance is not met. No higher
levels were enabled just to increase a gain. Higher native magnitude levels
need focused bounded physical validation before raising the current cap of 4.
The final ten seconds still included correction (31/70 nonzero speed samples),
so use the preceding stationary run, not this moving sequence, as the quiet-hold
evidence. We did not record video in either live run.

Physical manual override check: start native-speed tracking, then send the
registered Ctrl+Alt+Shift+Right nudge. UI reported "Local AI tracking off ·
manual override"; independent native GET read both speed axes 0 with no error.
Community UI was closed normally and no LINK360 processes remained. All four
protected file hashes matched the pre-test snapshot, including the real community
settings. Primary development and the archived ten points remain untouched.
OBS source was reactivated; Virtual Camera startup is verified separately
because the first immediate status can still report its asynchronous transition.

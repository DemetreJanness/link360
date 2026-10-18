# Feedback-wait motion continuity · 2026-10-18

Target: community software tracking. User reports a solid lock but slow,
stop/start movement after the six-point coverage test.

Static trace: TrackingPlanner returns null when FeedbackTravelBudget consumes
an off-centre correction. Both tracking loops interpreted that as Hold. The
continuous worker clears its pending goal on Hold and resets ServoAxis velocity.
Thus a valid off-centre subject waiting for delayed feedback followed the same
hard-stop path as an invalid/framed subject. This is a code-level mechanism;
its contribution to the user's optical jerk is not yet isolated.

Change: WaitingForFeedback distinguishes a budget-blocked valid correction
from a framed/invalid target. In that case both software loops leave the
existing bounded goal running. No goal is extrapolated or added, and this does
not refresh its 350ms expiry. Framed, missing, ambiguous, stale, reversed and
manual-stop paths retain their hold/cancellation behavior. Existing gain,
filtering, velocity/acceleration limits, map pairs and driver units are unchanged.
The hardware still receives whole-unit absolute commands; continuous gimbal-like
optical movement and faster catch-up are not established by this patch.

Validation: guarded review runner passed (20261018-143903-66b6e108), with no live
profile change; build zero warnings/errors. The delayed-loop fixture now retains
pending motion on feedback waits. An additional assertion confirms that every
100–450ms / 10–20FPS scenario actually exercises that path while maintaining
final-error and late-travel bounds; rebuilt default fixtures pass. Live A/B
movement/settling check remains pending. Primary/local build and both maps are
outside this change.

## Live result

The community run `room-test-20261018-214126-e81cabd3` completed with 480
samples, no stale/missing feeds, 36 outside-map samples and 285 movement
requests. Forced Acer produced 76 moves in 84 samples. The final ten seconds
had 19 moves despite a small room-target span (0.0155 X / 0.0355 Y). User judged
it “Still jerky or slow”: physical smoothness acceptance failed.

Sixteen feedback-wait samples exercised the fix. Mean command request/reply
was 15.9ms (max133.3ms), mean room inference26.0ms, observed Link frame age80.4ms.
These are application timings, not exposure-to-motor latency. The fix alone
clearly does not solve movement quality. Small relative position goals and
whole-unit driver targets remain; investigate the movement transport/goal
stream before further blind speed tuning. Do not infer that more calibration
points or faster GPU inference will remove this stepping.

Tracking/previews were paused after the run and OBS camera sharing restored.
Community six points, archived ten points and primary settings are unchanged.

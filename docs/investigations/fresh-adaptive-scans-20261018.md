# Fresh adaptive scene scans and separate publishing

Date: 2026-10-18. Target: LINK360 community Windows prototype on
`codex/community-build`, following the two incomplete new-placement scans.

## Question and evidence

The user requested fresh locations and measurements on every scan, with as many
useful points as the current scene supports. Earlier scans targeted five fixed
locations. They verified centre only, then rejected boundary alignment because
features did not surround the optical centre or too few matches survived masking.
These are observed failures, not evidence of a complete new-placement map.

Source inspected: `SceneRegistration`, `SceneScanPolicy`, `SceneScanController`,
`SceneScanDialog`, `PreviewSurface`, settings persistence and review-lab scripts.
This task changes existing source; no new vendor protocol or REA runtime claim.

## Implementation and contracts

- A new scan creates a GUID and archives its entire prior community profile in
  local AppData before clearing active scene/person measurements. Archives never
  feed target discovery. An incomplete generation remains explicit after restart.
- `SceneTargetDiscovery` uses inlier feature positions from the freshly verified
  centre view. It proposes 4–25 separated locations within current feature
  support, with enough spread to surround the centre. An unchanged room can
  rediscover the same coordinates; randomizing good points would not improve it.
- Every candidate still needs independent fresh optical measurement. Unsupported
  candidates can be skipped; a partial/one-sided/duplicated map cannot be saved.
  Each candidate starts from the freshly reverified centre. Existing direction,
  native-AI-off, frame-age, travel, cancellation and four-minute limits remain.
- `SceneReference.Targets` records the accepted plan. Older references without
  this field keep their legacy five-target validation. `PendingSceneScanId`
  blocks use of stale maps after an incomplete scan. Persistence failures restore
  the previous in-memory state; archived files are retained independently.
- Preview reset clears previous overlays. New rings represent this run's actual
  plan. The smaller dialog has room for both rows of buttons.

These are planar background correspondences, not a 3D point cloud or measured
person-depth calibration. Scene completion still requires new person points for
Acer recovery at the moved camera placement. Simulated points are not counted as
observations. Blank walls and movable-object masking can still prevent a scan.
No guarantee that arbitrary placements will self-calibrate successfully.

## Validation

`powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-review-lab.ps1`
built without warnings and passed default fixtures and actual tracking-loop
simulations. Fresh-scan fixtures cover changed/unchanged scenes, weak/one-sided
support, 4–25 targets, incomplete maps, repeated scans, stale completion,
serialization/restart, private archive preservation and failed writes.

`tests/BuildSync.Tests.ps1` uses disposable local bare repositories (no GitHub,
cameras or real settings). Passed: status-only fetch, separate publication,
staged-only commits, private-path and dirty-tree rejection, preserved main/local
branches, and atomic rejection when the remote community branch diverges. An
initial PowerShell function-name collision was corrected before publication.

UI fixtures rendered at 1120 and 920 pixel widths. Runtime optical quality for
the new adaptive scan is pending the user's empty-room test; fixture success
must not be called a successful physical scan or Acer recovery.

## Publishing workflow

`scripts/sync-builds.ps1` defaults to fetching/reporting incoming commits. The
explicit `-Publish` path builds/tests community and atomically pushes
`codex/local-development` and `codex/community-build`, without force, merging,
updating main, opening cameras or copying private data. Optional `-CommitMessage`
commits only explicitly staged reviewed source. Filename checks are not a secret
scanner. Contributor workflow and selective promotion are in `docs/REVIEW-LAB.md`.

The local-development commit remains `1cc6b3c`. Its binary and ten-point settings
hashes matched the pre-task baseline; the community ten-point seed profile also
remained unchanged before any new live scan. No captures or settings are staged.

## First adaptive live run and follow-up correction

The authorized run started at 20:30:35 UTC. The spoken countdown ran, both direct
feeds opened, native tracking was read as off, and the centre was measured.
The centre's verification had 84 inliers at 94% agreement. Discovery then
rejected the proposed coverage: targets did not surround the measured centre.
No completed map or Acer recovery result is claimed. The app announced failure
and stopped; OBS Link capture and Virtual Camera were restored and verified active.

The community profile has zero active old points and an explicit incomplete
scan ID; its ten previous points are preserved both in the profile's archive
field and a separate flushed AppData archive. The original development map and
binary remain untouched. This validates the fresh-generation failure lifecycle
on hardware, not a complete room map.

A synthetic regression reproduced a separate selection weakness: a supported
nearby feature cluster could be discarded by the 0.14-radius exclusion before
coverage validation. Lowering the candidate radius to 0.10 and ranking distances
from the actual measured centre retains that cluster. The fixture failed before
the fix and passed afterward. This is a demonstrated selection bug; whether it
caused this room's failure still needs a new live run. Optical match, hull-area,
spread, duplicate, cancellation and movement checks remain unchanged.

Discovery now reports its proposed locations in the private local diagnostic
report even when validation fails, so the next failure can be explained from
numeric evidence. No frame recording or external runtime service was added.

## Retry from a different initial PTZ direction

The next authorized retry started at 20:37:13 UTC and saved no anchors. The
camera reported pan 27, tilt -24, zoom 100 at both warm-up and scan start. The
live Link view faced below the useful shared background. The scan held its pose
through four failed centre-match attempts (17/19 matches, then concentrated
support). It never reached target discovery, so it did not test the nearby-point
correction. No evidence establishes why the pre-existing camera direction changed.
The new generation remains incomplete; both copies of the previous ten-point
map and the primary build hashes were verified unchanged. Sharing was restored.

The source previously attempted registration at whatever pose capture opened.
It now requests a bounded neutral wide view after the spoken countdown, before
fresh optical registration. This uses existing standard Link controls at pan 0,
tilt 0, zoom 100, not a firmware reset or a saved room anchor. Preconditions are
fresh direct frames from both cameras, successful empty-view inference, confirmed
native tracking off, unambiguous manual controls and valid ranges/steps/readback.
Initial travel is limited to 60 pan units, 40 tilt units and the existing zoom
range. Unknown flags, errors, excessive travel and cancellation stop the run.
A matching control readback is not optical calibration: the subsequent room
registration and coverage checks still determine whether any map can be saved.

Hardware-free fixtures verify the off-axis starting case and rejection for
people, detector errors, unknown/native-on tracking, manual flags, bad steps,
range mismatches, failed reads and ambiguous controls. The installed camera's
read-only probe reported expected range/step/manual flags. The new initial
framing step has not yet passed a physical scan; do not promote this evidence
to a completed new-placement room map or Acer recovery.

## Neutral-start live verification and simpler recovery contract

The 20:44:35 UTC scan verified the new starting-view step on the real camera:
readback moved from (30,4,100) to (0,0,100), and the live image showed the shared
background. Centre registration then verified 88 inliers at 94.6% agreement.
Discovery proposed five other locations but stopped because they did not
surround the centre. This was a coverage-policy rejection, not absent matches.
The scan stopped, announced failure, and local OBS capture/Virtual Camera were
restored. The original development settings hash still matched the baseline.

The user then explicitly requested fewer good points for Acer-to-PTZ correction,
without full-room complexity. The community contract now accepts a measured
partial area. It stops when at least four independently verified points pass
spacing, area, spread, match quality and RoomMap fitting. It may attempt up to
25 candidate locations. The outer points need not surround the centre; a narrow
line or repeated/weak measurements still cannot complete. Each individual
optical match must still be supported by surrounding fixed image features.

A completed scan saves its actual (Acer X,Y, Link pan,tilt) pairs directly as
RoomMapVersion 2 with the current scan identity. No person-point pass is required
for coarse recovery. Version 1 remains person-centred calibration; manually
starting that workflow archives the scene-derived map through the existing
version transition. Failed persistence rolls back both reference and recovery
pairs. Fresh scans still archive/clear all prior active measurements first.

These background pairs are coarse guidance, not 3D depth or guaranteed
person-centre accuracy. Existing Delaunay interpolation refuses targets outside
the measured hull. The hybrid tracker prioritizes a valid Link lock and uses
Acer for reacquisition; Link image feedback refines final framing. Different
subject/background depths can still produce parallax error, to be assessed in
live recovery tests. No guessed or simulated point is counted as measured.

Offline checks passed with zero build warnings/errors, including partial-area
acceptance, four-point completion, rejection of three/duplicate/collinear/weak
points, wide-view gating, exact recovery-pair persistence/reload, no
extrapolation, preserved previous maps and write-failure rollback. Shared
tracking-loop simulations also passed. A physical run of this simpler contract
is in progress; offline success does not establish recovery quality.
The first simplified live run (20:49:28 UTC) passed target discovery and moved
along seven optically matched positions toward the first candidate, with
48–88 inliers. It stopped at secondary verification because the frame-difference
check saw movement or exposure change. This establishes useful scan motion,
not a completed map. The controller now holds and retries that verification up
to four times; repeated candidate failure skips that candidate. It keeps fresh
frame, empty-view, optical stability and geometry checks, and does not count a
rejected point. Centre failure still stops the scan. The default and shared-loop
suites passed after this bounded retry change.
## Completed four-point hardware scan

The next live run (20:51:39–20:53:08 UTC) completed successfully. It independently
verified four fresh Acer-to-Link pairs, with 41–88 inliers per saved point and
69–100% agreement. Two transient verification failures were held/retried rather
than aborting. Return-to-centre verification had 96 inliers and 97% agreement.
The local report explicitly records `previousMeasurementsReused: false`.

The saved community profile contains four active points, RoomMapVersion 2,
matching scene/map identities, no pending scan and ten preserved previous
points. Primary settings/executable and the separately archived original
community profile still match their baseline hashes. Numeric reports remain in
local AppData; no room images, settings or full private reports are committed.
This verifies automatic measurement and persistence. Live subject recovery with
this background-derived partial map remains the next test, not yet a passed
result. OBS sharing restoration is tracked separately from scan completion.
## First recovery check with the four-point map

The authorized 60-second test completed at 20:55:11 UTC with both direct feeds
fresh throughout and no missing/ambiguous Acer samples. The initial 15-second
seated phase issued zero movement requests. During the forced Acer interval
(35–40 seconds), all 22 sampled positions were outside the measured hull and
issued no move. Therefore this run did not physically verify Acer correction.
The Link subsequently returned to centred framing. The final ten seconds still
contained 18 movement requests; zero hunting or final stationary smoothness is
not established. No test video was recorded in this run.

The immediate limitation is coverage of the user's movement positions, not the
point-count gate or a missing recovery-map connection. Future work should target
additional measured coverage where the test reports outside-map positions, or
validate coarse recovery from an in-map position. Do not claim that relaxing
coverage proved full-room recovery. Keep the four measured pairs; do not reuse
the old mount's ten points or fabricate extra measurements. LINK360 tracking and
previews were paused after testing and OBS capture/Virtual Camera restarted.
Work stops before the user's 40%-remaining usage threshold (last read: 41%).
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

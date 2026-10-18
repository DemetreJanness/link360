# LINK360: two maintained builds

| Build | Local and GitHub branch | Purpose |
| --- | --- | --- |
| Local development | `codex/local-development` | Original workspace and known local build |
| Community review | `codex/community-build` | Isolated test build and reviewed contributions |

Both branches are published to https://github.com/DemetreJanness/link360.
`main` remains a shared baseline; publishing does not merge either build into it.
No API service, telemetry, automatic runtime download or background updater is
installed. Git operations transfer source only. Builds and camera tests run locally.

## One-command updates

Run from the community worktree. Check for incoming code without executing it:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\sync-builds.ps1
```

After reviewing changes and committing them, close the community app and publish:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\sync-builds.ps1 -Publish
```

This fetches GitHub, checks both worktrees are clean, builds/tests community with
the local NuGet cache, and pushes the two named branches atomically. It never
pulls, merges, resets, force-pushes, copies settings or launches the cameras.
A remote conflict stops publication for review. Local development is published
at its current commit without rebuilding or changing its files. Its existing
validation remains separate from the community test result.

Optionally stage an explicit list of reviewed source files with `git add --
<paths>` and pass `-Publish -CommitMessage "Describe the reviewed change"`.
Only staged files are committed; all unstaged/untracked changes block the run.
Generated/private paths are rejected. This is a filename guard, not a secret
scanner: review the staged diff and never stage settings, recordings or keys.
A failed test leaves that local commit available to fix, without pushing it.
Git uses your configured commit identity and credentials.

No scheduled task was created. Run this command when changes are ready, or ask
Codex to review and publish. Unreviewed incoming code is never executed by the
status command. `-IncomingBranch <branch>` selects a different proposal to report.

## Instructions for a contributor

> Fetch the repository and branch from `origin/codex/community-build`. Create a
> new branch such as `codex/kairos-scan-improvements`. Keep changes focused and
> include tests and a short explanation of the failure and expected behavior.
> Push your branch and open a pull request targeting `codex/community-build`.
> Keep all camera processing local. Do not commit credentials, settings, room
> points, recordings, models or captures. Preserve `CommunityProfile`, the
> single-controller mutex, movement limits and manual stop behavior. Do not
> push directly to `main` or `codex/local-development`.

Existing proposal: `codex/tracking-pipeline-efficiency`, incoming
`6c1a484a2bdaecb022aca7da1c0d7b1b27d26e13`; community import `148abb0` followed
by independent timestamp corrections and integration tests. See
[incoming review](investigations/community-pipeline-review.md).

## Review and integrate a proposal

Fetch, inspect the actual diff and any changes to dependencies/build scripts
before running it. In the clean community checkout only:

```powershell
git fetch --no-tags origin
git diff HEAD...origin/codex/kairos-scan-improvements
git merge --no-ff --no-commit origin/codex/kairos-scan-improvements
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test-review-lab.ps1
```

Resolve conflicts deliberately, retain profile isolation, then commit the
reviewed merge. `git merge --abort` abandons an uncommitted merge. Record the
incoming SHA, fixture results and physical test results separately. Fixtures do
not establish optical movement quality. Rebuild after any conflict resolution.

To promote an accepted fix into local development, use a separate integration
branch from its current head and select only accepted implementation/test
commits. Re-test against local development, then merge that integration branch.
Do not merge the entire community branch: it contains profile/lab infrastructure.
Promotion remains an explicit decision, independent of publishing both builds.

## Build, launch and private data

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test-review-lab.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\start-community.ps1
```

Tests restore from the local NuGet cache with no online feeds or security audit,
build to ignored `artifacts/review-lab/bin`, run default fixtures and actual-loop
simulations, and save numeric reports. Missing cached dependencies fail restore.
The community window is labelled **Community review build**. Settings are at
`%LOCALAPPDATA%\LINK360-Community\settings.local.json`; reports and optional test
clips are in that profile's `diagnostics` directory. The primary app keeps its
executable-local settings and `%LOCALAPPDATA%\LINK360` diagnostics.

A one-time private seed copy initialized community. No subsequent settings sync
occurs. A fresh scan archives its prior community map under `preserved-maps`,
clears active old measurements, and creates a new scan identity. Failure or
cancellation leaves the new map incomplete, never silently restoring old points.
The original ten-point map and primary binary remain separate.

The shared `Local\LINK360.Desktop` mutex allows one controller at a time.
Cameras, OBS, hotkeys and Discord remain shared Windows resources; hardware tests
are sequential. Source/build/profile separation is not an untrusted-code sandbox.


### Automatic correction points

The community scan saves once four verified points cover a usable area. Full-room
coverage is optional. Each saved Acer image coordinate is paired with a measured
Link pan/tilt direction and published atomically as a version-2 coarse recovery
map. No second person-point pass is required. Recovery stays inside the measured
point hull; Link feedback refines subject framing. Scene/background depth can
differ from the person's depth, so live recovery testing remains necessary.
Every scan starts fresh and keeps the prior map archived. Local development is
unchanged.

# LINK360: two maintained builds

| Version | Local branch | Source | Purpose |
| --- | --- | --- | --- |
| Local development | `codex/local-development` | Original `Link` workspace | Continue our development independently |
| Community review | `codex/community-build` | This separate managed worktree; incoming changes from `origin/main` | Build and evaluate the friend's committed findings |

Remote: https://github.com/DemetreJanness/link360.git. The friend commits to
GitHub `main` on their end. There is no review API integration on this machine.
The published starting point is `1cc6b3cd180d0915633fd7ad1e7ec112455c53ed`.

The local development branch has no upstream, so a plain `git pull` there cannot
bring in community changes. The community branch tracks `origin/main` and also
contains local review infrastructure. Do not push this branch over GitHub main.
Neither checkout auto-updates, merges, launches, or publishes anything.

## Build and check the community version

From this checkout:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test-review-lab.ps1
```

This uses the local NuGet cache, builds the desktop project and default fixture
tests, and runs those tests without camera hardware, OBS or the vendor app.
Outputs and logs are ignored under `artifacts/review-lab/`. Temporary fixtures
stay under `.rea-tools/`. Missing cached packages fail restore rather than add
an online feed. Dependency security auditing is not part of this offline run.

When a camera test is wanted, first close the normal LINK360 application, then:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\start-community.ps1
```

The community window says **Community review build** and **COMMUNITY REVIEW**.
It reads and writes `%LOCALAPPDATA%\LINK360-Community\settings.local.json` and
stores diagnostics/video under that profile's `diagnostics` folder. The normal
app retains its executable-local settings and `%LOCALAPPDATA%\LINK360` logs.
A one-time private copy of the original settings seeds the community profile;
subsequent changes do not synchronize in either direction. Never commit either
settings file, the room map, recordings or captures.

The shared `Local\LINK360.Desktop` mutex deliberately allows only one interactive
LINK360 controller at a time. Cameras, hotkeys, OBS and Discord are shared
Windows resources, not virtual copies. Hardware tests remain sequential.

## Receive committed changes from the friend

In the community checkout only:

```powershell
git fetch --no-tags origin main
git log --oneline HEAD..origin/main
git diff HEAD...origin/main
```

Inspect the new commits, build scripts, dependencies and test entry points
before executing their code. To bring reviewed commits into the community build:

```powershell
git merge --no-ff --no-commit origin/main
```

Resolve conflicts deliberately, retaining `CommunityProfile` settings/log paths,
the review label and the single-controller mutex. Build and run the local checks.
Commit the resulting merge only after its scope and results are understood.
Use `git merge --abort` to abandon an uncommitted merge. Do not force-reset the
community branch to remote main; that would remove the local isolation layer.

The check report records the local SHA and whether its tree had uncommitted
changes. Record the incoming SHA and relevant before/after behavior when testing
a proposal. Default fixtures do not establish physical movement quality.

## Bring selected fixes into local development

Use an integration branch from the current local development head and select
only the implementation/test commits we accept. Re-run checks against our
current development code, then integrate that result. Keep independent fixes
in separate commits. Leave community profile and lab setup commits in this
checkout; do not merge the whole community branch into local development.

This is source/build/data separation, not an OS sandbox for untrusted code.
Review code before running it. The ten original map points stay in the normal
app's private settings; community tests cannot change that file through the
community settings path.

## Current test candidate

On October 18, the user requested testing the friend's separate branch
`codex/tracking-pipeline-efficiency` rather than waiting for it to reach `main`.
Incoming SHA: `6c1a484a2bdaecb022aca7da1c0d7b1b27d26e13`; community import: `148abb0`.
It is applied only here, followed by a separate timestamp correction and
hardware-free integration tests. See
[incoming review and validation](investigations/community-pipeline-review.md).
The test runner now includes the actual-loop simulation in addition to the
default fixtures. Local development and remote branches were not modified.

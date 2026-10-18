# LINK360 review lab

This checkout is for evaluating outside review findings and proposed patches.
The reviewer runs their tools on their own computer. No external review API or
credentials are needed by LINK360.

Baseline: `1cc6b3cd180d0915633fd7ad1e7ec112455c53ed` (public `main` when this lab
was created). The lab branch is `codex/review-lab`. The normal development
checkout and installed application are separate and remain unchanged.

## Run the baseline checks

From this checkout in PowerShell:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test-review-lab.ps1
```

The command restores from the existing local NuGet package cache, builds the
Windows desktop project through the tests' project reference, and runs only
the default fixture tests. It does not launch the application or pass any
hardware-probe flags. Logs and build outputs stay in this checkout under
`artifacts/review-lab/`; fixture files stay under its ignored `.rea-tools/`.
A missing cached dependency causes restore to fail; this command does not
add an online feed. Dependency security auditing is not part of this offline run.

No personal settings, room points, videos, captures or API keys were copied
into this checkout. The default suite uses synthetic room points and local
bundled models. A passing result does not prove physical tracking smoothness.

## Receive and evaluate a proposed fix

Ask the reviewer to send a GitHub pull request (preferred), a branch/commit
URL, a patch, or a written finding with file locations. A useful proposal states
its baseline commit, what is wrong, what changes, and how it was tested. Keep
independent fixes in separate commits, especially motion, detection and UI work.

1. Create a proposal branch from this lab, for example
   `git switch -c codex/review/steady-hold`.
2. Fetch the specified proposal into the lab. Inspect the diff, build scripts,
   package changes and test entry points before executing anything. For a PR
   against this repository, `git fetch origin pull/<number>/head` fetches its
   commits without changing either working tree. Inspect `FETCH_HEAD`; apply
   only the intended commits. Do not merge or pull automatically into `main`.
3. Run the default checks above and focused tests for the actual finding.
   Record the reviewed SHA, before/after observations, failed cases and limits.
   Keep calibration and media private. Do not treat simulated success as a
   physical camera result.
4. For hardware testing, schedule a separate session: close the normal app,
   isolate the experimental diagnostics/settings first, and use one camera
   controller at a time. Preserve the original ten-point settings. Never
   replace them just to make an experimental test pass.
5. Bring back only accepted implementation/test commits on an integration
   branch from the current development head. Re-run checks there and keep a
   revertable commit per fix. Leave this lab's setup commit out of that selection.
   No automatic integration or publication is configured.

## Isolation limits

A Git worktree separates source, build outputs and executable-local settings;
it shares Git objects, installed dependencies and the Windows hardware session.
It is not an OS sandbox for untrusted code. Review proposed scripts and code
before building or running them.

Do not launch `scripts/start-link360.ps1` or the lab's `LINK360.exe` for routine
code review. The current UI registers global hotkeys and accesses camera/OBS
state at startup, and application diagnostics use the shared local LINK360
AppData directory. Those runtime paths have not been altered by this setup.
The supplied test command uses the existing camera-independent default suite.

There are no automatic camera tests, background review services, merges or
uploads. The lab can remain here while normal development continues elsewhere.

# Architecture integration evidence

Date: October 13, 2026.
Target: Insta360 Link; controller 2.2.5.14; firmware v1.4.8_build2 reported by
the user. Question: how to apply specification v1.0.0 to ongoing LINK360 work?

## Observations

Read the supplied specification, AGENTS.md, product direction, README,
`direct-camera-control.md` and existing control/inspection scripts.
The newer direct-control record supersedes the initial inventory's readiness
statement: it records REA CLI readiness and standard-control tests. No new REA
readiness check or hardware write was performed for this integration.

Command: `Get-PnpDevice -PresentOnly` filtered by Insta360/Link friendly name.
The sandbox denied CIM access; the approved escalated read succeeded. Camera
and media entries reported OK, VID 2E1A / PID 4C01. Device instance identifiers
are omitted here. This is Windows inventory evidence, not USB capture.

Command: `Get-Command cargo,rustc -ErrorAction SilentlyContinue` returned no
entries on the inspected PATH. Rust installation elsewhere remains unknown.

## Conclusions and implementation implications

Preserve the standard Windows-control prototype, follow its recorded validation
limits, and investigate extension units before requiring raw USB. No new packet
format was recovered. The user's architecture is adopted as production direction
and staged evidence gates in `../engineering-architecture.md` and AGENTS.md.

The new guidance distinguishes UI lifecycle failure from Rust panic handling,
requested state from confirmed state, and architecture goals from tested
latency/privacy/recovery guarantees. Rust migration must preserve proven
contracts. UI, automation, routing and AI remain gated future work.

## Validation and uncertainties

Documentation changes only; existing scripts and native source were preserved.
Cross-checked current scope and validation labels against the direct-control
record. Physical motion, firmware readout, reconnect, concurrency, driver audit,
and native decompilation remain unverified or pending as recorded there.

# Build alignment and retired vape experiment

Date: 2026-10-18. User requested all builds use the newest settings and abandoned
vape mode. This authorizes promotion of the tested community tracking code to
local development, superseding the earlier hold on local source/binaries.

Both branches now share the current tracking implementation, including the
fresh paired calibration wizard and visible-body horizontal reacquisition.
Transport, native speed policy and calibration measurements are not retuned.
The vape experiment is reverted, including its UI, detector, entry point, tests
and voice asset. History retains its failed live acceptance evidence.

A build-time Link360Profile selects Local (default) or Community explicitly.
Local retains its executable-local settings and LINK360 AppData diagnostics;
Community retains LINK360-Community settings/diagnostics and its visible label.
The review build script supplies Community. Invalid profile names fail the build.
This removes the prior obstacle to sharing source without mixing private data.

Inspected settings share transition=1400ms and Wide framing. No private map or
preset overwrite is needed to align the latest implementation/defaults. The
original local ten points, community six points and its archived ten remain
byte-identical. The 25-point debug calibration remains a consumed diagnostic
session, not silently promoted to either persistent map. Those maps remain
placement-dependent; code promotion does not validate old camera placements.

Previous local exe/dll/runtime config/settings were privately backed up before
replacement. Git publication covers source only, not settings or observations.
GitHub main remains the collaborator's shared baseline; the two maintained build
branches are updated together without merging unseen main contributions.

Validation and deployment results are appended after completion.

Community guarded checks 20261018-162252-29e5952a passed, zero warnings/errors. Final explicit Community build and profile probe passed after profile validation was added.

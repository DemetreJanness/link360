# REA for LINK360

Reviewed on October 13, 2026.

## Purpose

Use REA to understand a selected reference feature, retain evidence for how it
works, and turn that understanding into tested LINK360 behavior.

REA exposes local analysis through MCP and a CLI. Its capabilities include
JavaScript/Electron structure and IPC, website observations, native binary
inspection, static .NET inspection, and saved network capture analysis.
Provider and platform requirements vary by target.

## Current workspace status

Update: REA 6.4.0 now runs from `.rea-tools/node_modules/rea-agents/scripts/rea.mjs`.
It was installed with the available pnpm runner. A project-local Ghidra 12.1.4
and Temurin JDK 21.0.12.1 were downloaded for native analysis. Java's SHA-256
was checked against the release API checksum. REA PE resource inspection has
succeeded; native engine tracing is still being validated. No global Codex
MCP registration was changed. The following bullets describe initial discovery.

- This workspace initially contains only a Git directory; no LINK360 application
  source, product specification, or reference target has been supplied.
- Node.js v24.19.0 is available and falls within REA's documented runtime range.
- npm was not found on the current shell's PATH.
- REA installation and MCP connectivity have not been verified.

## Setup route

Once npm/npx is available, the official setup command is:

```powershell
npx rea-agents@latest setup --client codex --dry-run
```

Review the proposed paths and configuration changes, then run setup without
`--dry-run` to apply them through its approval flow. Restart the selected agent
after configuration changes. Setup is client-wide; this project workflow file
does not register an MCP server or install REA.

Choose the analysis engine only after selecting a target. Static JavaScript
analysis does not need a native decompiler. Native analysis may require an
existing Hopper, Ghidra, or IDA installation and additional provider setup.

## First investigation

Supply a reference app or URL and the feature LINK360 should implement. Record
the expected input/output behavior, inspect the relevant evidence, specify the
LINK360 implementation, and validate it against those expectations.

Suggested record structure:

```text
Target and version:
Question and desired LINK360 behavior:
Artifacts and reproduction steps:
Evidence (locations, commands, observations):
Observed facts:
Inferences and limitations:
LINK360 requirements and implementation:
Validation and remaining questions:
```

## Sources

- Repository: https://github.com/morluto/rea
- Setup: https://github.com/morluto/rea/blob/main/docs/installation.md
- CLI and evidence guide: follow the current link in the upstream README.

Consult current upstream documentation before executing setup or analysis;
available commands and provider support can change.

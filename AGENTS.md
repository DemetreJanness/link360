# LINK360

Use REA (Reverse Engineer Anything) as an investigation tool when a LINK360 task
requires understanding an existing application, website, protocol, or artifact.
Upstream: https://github.com/morluto/rea

## Investigation workflow

1. Identify the target, the specific behavior to understand, and the intended
   LINK360 outcome. If these are missing, ask before choosing a target.
2. Inspect available source and static artifacts first. Use REA when its tools
   provide additional evidence; select the provider appropriate to the target.
3. Trace the relevant inputs, state changes, boundaries, and outputs. Record
   file locations, tool commands, observations, and limitations.
4. Separate observed facts from inferences and untested hypotheses. Do not
   describe static findings as verified runtime behavior.
5. Translate findings into LINK360 requirements and an implementation using
   this project's architecture. Preserve applicable notices for reused code.
6. Verify the required behavior with representative inputs and failure cases.

Keep investigation records under docs/investigations/. Include target/version,
question, evidence, conclusions, uncertainties, implementation implications,
and validation results. Do not commit credentials or sensitive capture data.

REA is an optional development tool, not an application runtime dependency.
Do not claim REA is installed or connected until a readiness check succeeds.
Follow the project's existing tooling as it develops; REA does not determine
LINK360's product scope or technology stack.

## Engineering architecture and evidence gates

Apply `docs/engineering-architecture.md` alongside the product direction to
current investigations, prototypes, and future implementation work.

- Keep runtime processing local: no accounts, telemetry, uploaded frames, or
  cloud dependencies. Development documentation lookup is not runtime egress.
- Preserve the current Windows control prototype and its evidence. Rust/Tauri
  is the intended production direction, not a reason to discard verified work.
- Investigate the installed Windows camera interfaces before choosing raw USB.
  Do not replace camera drivers or dispatch speculative payloads to satisfy a
  proposed library choice. Keep transport behind a capability-aware adapter.
- Label capabilities as proposed, statically observed, driver-verified, or
  physically verified. A successful readback does not prove optical motion.
- Validate device identity, command units/ranges/steps, flags, timeouts, errors,
  cancellation and disconnect behavior before promoting a control to production.
- UI, presets, hotkeys, automation, video routing and local AI follow the staged
  gates in the architecture document. Do not describe them as implemented.
- Record protocol/API contract changes and downstream effects. Preserve proven
  contracts where needed; select firmware-specific behavior only from evidence.
- Do not claim deterministic latency, crash isolation, recovery, reconnection,
  or OBS/Discord concurrency until the corresponding tests have passed.

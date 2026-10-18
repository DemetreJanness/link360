# Vendor movement path · 2026-10-17

Target: installed Insta360 Link Controller 2.2.5.14 executable.
SHA256: `28b9242b275abc8e4a4d11c2997919649eaeddc59e5889ec189dc2a031d8f942`.
Question: does its velocity-style UI path expose a usable smoother camera
control than LINK360's verified whole-unit absolute Windows controls?

REA 6.4.0 scoped Ghidra readiness succeeded with Ghidra 12.1.4. This establishes
CLI provider readiness, not a connected live MCP session. Optional provider
checks were not all healthy. Artifacts are ignored development files:
`.rea-tools/rea-motion-readiness.json` and `rea-motion-function.json`.

Static string inspection found QML handlers calling
`movePanTilt(speedPan,speedTilt)` and stop `movePanTilt(0,0)`. These are UI
function names and values, not a proven transport payload. Evidence:
`.rea-tools/vendor-motion-strings.json`, `vendor-motion-xrefs.json`, and
`scripts/find-vendor-xrefs.mjs --motion`.

Candidate addresses from string references and PE unwind boundaries:

- CameraMonitor::MovePanTilt: `0x140c58a80`, 528 bytes.
- CameraInsta::setPitch: `0x140c60240`, 1893 bytes.
- CameraInsta::setRoll: `0x140c609b0`, 1269 bytes.
- CameraInsta::addPanTilt: `0x140c5e350`, 1240 bytes.

REA's `function <exe> 0x140c58a80 --provider ghidra --format json` timed out
at 330000 ms. The existing installed Ghidra project was then inspected with
`analyzeHeadless ... -process <exe> -noanalysis -postScript
NativeFunctionInspect.java 0x140c58a80 528 <output> -max-cpu 2`.
The static result is `.rea-tools/native-motion-monitor.txt`. It principally
shows a state guard and logging; no velocity transport or units were recovered.
No target executable was launched and no new USB/camera command was sent.

Remaining work after the user resumes: follow the actual controller dispatch
and transport, distinguish speed input from repeated position increments, and
establish supported units, stop/cancel semantics and hardware behavior before
adding any adapter. Existing native-on selector gates remain in force. This
investigation does not establish a smoother runtime control capability.

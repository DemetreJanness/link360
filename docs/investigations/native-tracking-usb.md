# Original Link native tracking investigation · 2026-10-14

Target: Insta360 Link Controller 2.2.5.14; original Link VID2E1A/PID4C01,
firmware reported by user as1.4.8_build2. Question: restart on-device AI after
Acer-guided torso recovery, without the vendor application running.

## Observed evidence

- REA6.4.0 PE-resource parser successfully inspected the219,408,328-byte vendor
  executable with an explicit256MiB limit. Evidence ID:
  `ev_f48a40ee3d1ab55dc5f4b182835562e390f857bed904fe315fff09b8aa6977c6`.
  Executable SHA256:
  `28b9242b275abc8e4a4d11c2997919649eaeddc59e5889ec189dc2a031d8f942`.
  This is static evidence; it does not establish a live REA MCP connection.
- `scripts/isolate-vendor-investigation.ps1` created and verified scoped outbound
  block rules for the controller and its crashpad reporter. All three Windows
  Firewall profiles were enabled. Two earlier UAC launches were canceled;
  the subsequent run succeeded. This verifies the rules, not a full system-wide
  traffic audit. The rules remain installed; the vendor application was closed.
- Wireshark/USBPcap1.5.4.0 captured the uniquely enumerated Link onUSBPcap2.
  Its address was40 during these captures; the helper discovers the address each
  time. Development capture filters before disk: endpoint0, UVC class controls,
  configuration descriptor only. No video, audio, other devices, string/serial
  descriptors or network packets are retained. Duration<=120s; file<=2MB.
  Raw captures stay in ignored`.rea-tools/native-usb/`.
- Early stdout capture returned no packets. Running the helper elevated with
  a named-pipe output succeeded. First useful run:758 retained control packets,
  7205 discarded packets. Repeated toggle run:794 retained,7206 discarded.
- The injected configuration descriptor identifies extension entity9, GUID
  `faf1672d-b71b-4793-8c91-7b1c9b7f95f8`.
- Repeated UI toggle: UVC SET_CUR, interface0/entity9, selector2, length52.
  Vendor off payload has first uint32=0; on=1. Bytes4..37 are zero in these
  observed requests. SET tail contains three little-endian0x0E1A sentinels.
  GET tail changes with live PTZ telemetry. Copying that tail into an experimental
  SET did not verify; its valid write semantics are not established.
- Native-on is preceded by SET_CUR selector21, length8. Two captured values
  decode as finite floats inside[0,1]. They appear consistent with subject
  selection coordinates, but the semantic interpretation remains unverified.

## Implementation and validation

`src/native/NativeTracking.cs` uses the installed SDK's IKsTopologyInfo/IKsControl
definitions. It discovers a unique device-specific topology node supporting the
observed extension GUID and52-byte selector2; it does not assume USB entity9 is
the Windows topology node index. The worker uses bounded processes and rejects
mixed extension/PTZ request types. Unknown configuration bytes block writes.

An initial COM array declaration caused a worker crash. Corrected to LPArray
with explicit data size; subsequent reads succeed. The first experimental SET
copied GET's changing tail and did not verify. Corrected to the vendor-observed
SET sentinel template. Immediate reads sometimes return0xFF; the worker now
polls for a settled value for at most3s. With a verified active direct video
stream, a native-off request returned a confirmed off state. Native-on without
the preceding selector21 did not settle; cleanup returned a confirmed off state.

Automatic approval review rejected a test of dynamically constructed selector21
coordinates, both before and after the user's explicit bounded-test approval.
The stated reason was unverified payload semantics/speculative device control.
No selector21 write was executed by LINK360. Native activation is explicitly
gated before hardware access, with a regression fixture. The UI accurately
offers manual palm activation only; automatic native restart is not shipped.

## Next investigation

Use static REA evidence or read-only comparisons to establish selector21's data
layout and target semantics. Do not bypass the rejected write through another
tool or vendor automation. Once validated and permitted, test activation with a
fresh, unique local target, verify settled state and physical following, then
test off→Acer torso recovery→native restart. Failures must stop movement and
remain visible; no two controllers should issue PTZ commands concurrently.

Microsoft references:
[extension-unit info](https://learn.microsoft.com/en-us/windows-hardware/drivers/stream/ksproperty-extension-unit-info),
[KSPROPERTY flags](https://learn.microsoft.com/en-us/previous-versions/ff564262%28v%3Dvs.85%29).
SDK definitions read locally from10.0.22621.0`vidcap.h`, `ks.h`, `ksmedia.h`.

## Focused static continuation

REA 6.4.0's Ghidra provider passed its engine prerequisites (Ghidra 12.1.4,
Java 21) but `rea.mjs function <vendor.exe> 0x140c50bd0 --provider ghidra`
returned `provider_timeout` after 330 seconds. The broader doctor did not pass
all optional providers/client setup; no live REA connection is claimed.

Available PE/string artifacts were inspected first. `find-vendor-xrefs.mjs`
uses PE sections and unwind metadata to locate candidates; raw xrefs are only
candidates until checked in disassembly. A direct installed-Ghidra fallback
then succeeded with bounded function disassembly/decompilation, without running
the application:

```text
analyzeHeadless.bat <workspace>/artifacts/native-analysis tracking-1791971970087
 -process "Insta360 Link Controller.exe" -noanalysis
 -scriptPath <workspace>/scripts/ghidra
 -postScript NativeFunctionInspect.java <address> <seed-bytes> <output-file>
 -max-cpu 2
```

`scripts/ghidra/NativeFunctionInspect.java` and
`scripts/find-vendor-vtables.mjs` retain the repeatable inspection method.
Ignored `.rea-tools/native-*.txt` holds pseudocode/disassembly; proprietary
implementation is not copied into product source. Ghidra project stays ignored.

Static findings, for the same executable hash above:

- `setAutoTrackingEnabled`, function `0x140c4d6e0`, calls the SDK Boolean setter
  with enum 8. Its extension-controller slot 32 (`0x140e84d40`) accesses selector
  27 and changes a configuration bit. This permission setting is separate from
  the active mode packet observed on selector 2. It should not be presented as
  a verified runtime toggle.
- `selectRect`, `0x140c4b130`, passes a coordinate pair through SDK slot 19;
  its mode-1/type-2 path zeroes rectangle width/height. Other paths select the
  point and enter mode 1. This supports the hypothesis that native acquisition
  needs target selection before active tracking.
- The slot-19 writer, `0x140e90b70`, assembles four 32-bit rectangle fields and
  passes selector `0x15` (21), SET flags and the captured extension GUID to its
  device-control helper. Original-Link capture showed only 8 transmitted bytes.
  The helper's device-specific length and normalized coordinate interpretation
  still require validation; the static structure alone does not settle them.
- The mode writer `0x140e90df0` includes a PID `0x4c01` path with the observed
  sentinel values and mode byte. Unresolved decompiler types remain limitations.
- Rectangle validation `0x140c44b40` includes other product-ID paths. Its checks
  cannot establish original-Link bounds without following the applicable path.
- Early string-neighborhood candidates in `native-auto-tracking-static.txt`
  and `native-tracking-speed-static.txt` were neighboring mirror/permission
  functions, not the named behavior. Superseded by exact xref/vtable results;
  no protocol conclusion is based on those early candidates.

Read-only Windows driver probes found standard relative pan/tilt controls and
relative zoom BASIC_SUPPORT unavailable (`0x80070490`), while absolute pan
support succeeded. This limits the queried driver route; it does not prove
firmware lacks a velocity command. See `scripts/check-relative-capabilities.mjs`.

These findings strengthen the target-selection interpretation but do not verify
automatic restart physically. No new selector-21 write was attempted overnight.
The activation gate remains. A person-present bounded test, settled-state check,
then an off→Acer recovery→native restart test remain outstanding, subject to the
existing approval-review boundary.

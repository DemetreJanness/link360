# Native direction/speed movement · 2026-10-18

Target: installed Insta360 Link Controller 2.2.5.14, executable SHA256
28B9242B275ABC8E4A4D11C2997919649EAEDDC59E5889EC189DC2A031D8F942.
Question: does native movement use a different transport than LINK360's repeated
whole-unit absolute targets? User reports much smoother built-in tracking.

## Observed USB evidence

Both vendor executable and crash reporter outbound Block rules were verified
against their exact installed paths; all Windows Firewall profiles enabled.
Community tracking/previews were paused, its process closed, and OBS source /
Virtual Camera temporarily released before vendor launch. Neither room map was
edited. No LINK360 native-motion writes were dispatched.

Local USBPcap control-only capture:
`.rea-tools/native-usb/tracking-1792359934763.pcap` (ignored, not published).
946 saved packets; 4,969 other packets dropped. PCAP validated with Wireshark
4.6.9 and REA inventory, SHA256
d91dd6c395c92f34006a0ed63d0a5dd60fb1ffd23a6779539b9e6ed795424ad8.
Only endpoint-zero UVC class requests and configuration descriptors reach disk;
no video/audio stream payloads. The audio protocol label in the inventory comes
from the injected device configuration descriptor.

Native right/left direction clicks yielded extension entity9 selector22
(wValue0x1600, wIndex0x0900), four-byte SET_CUR writes:

| Native UI action | Payload hex | Completion |
| --- | --- | --- |
| Right press | 01 04 00 01 | USBD success |
| Release (~108ms later) | 00 01 00 01 | USBD success |
| Left press | FF 04 00 01 | USBD success |
| Release (~117ms later) | 00 01 00 01 | USBD success |

GET_LEN returned0400 (four bytes); GET_CUR returned00030003 initially,
then the observed neutral00010001. This is a distinct command family from the
Windows absolute pan/tilt steps. These short pulses do not establish sustained
motion smoothness, physical speed units or fail-safe behavior.

The first capture ended with an EBADF parser cleanup race after writing its
completed file/metadata. All946 complete packets are readable. A local ignored
copy adds a closed-file guard for future capture; product source was unchanged.
A second capture saved zero packets while Windows UI access failed; it provides
no further movement evidence.

## Static trace

Existing Ghidra12.1.4 project reused without whole-program analysis:
`artifacts/native-analysis/tracking-1791971970087`.
Commands: analyzeHeadless -process vendor.exe -noanalysis, existing
scripts/ghidra/NativeFunctionInspect.java with these exact addresses / bounds.

- CameraInsta::addPanTilt 0x140c5e350 /1240bytes assembles a speed magnitude
  and separate direction for both axes. Numeric data reads give conversion
  int(abs(input)*9+1), direction zero when abs(input)<=0.000001, otherwise
  1 orFF. Caller input bounds remain unverified; this does not prove a safe
  overall speed range or degrees/second.
- SDK dispatch thunk0x140e72c80 loads vtable offset0xC0 (slot24).
  Its twelve bytes were inspected directly as a small x64 thunk. An initial
  over-wide Ghidra function seed overlapped another function; no conclusion is
  taken from that failed decompilation.
- WinUVCCameraExtendController slot24 is0x140e8fed0 /220bytes. Static writer
  packs [pan direction, pan magnitude, tilt direction, tilt magnitude], selects
  property0x16 with SET/TOPOLOGY flags0x10000002, and uses the extension helper.
  This agrees with the captured four-byte packets.
- Getter thunk0x140e73e50 uses slot25 (offset0xC8).
- Embedded UI handlers send movePanTilt(0,0) when the panel becomes hidden.

Private static outputs: .rea-tools/native-add-pan-tilt.txt,
native-set-pitch.txt, native-speed-selector-writer.txt,
native-speed-read-dispatch.txt. Proprietary pseudocode is not copied into
LINK360 source or this repository.

## Updated REA

Installed6.7.0 in isolated ignored .rea-tools/rea-current; original6.4.0 and
agent configuration left intact. Official npm package installed with scripts
ignored, using an integrity-checked local npm bootstrap. Scoped doctor passed
for Ghidra/this target. No live MCP connection is claimed.

REA inspect-pcap-capture succeeded with explicit REA_TSHARK_COMMAND and
REA_CAPINFOS_COMMAND paths. Evidence saved locally in
.rea-tools/rea-670-motion-pcap.json; readiness in
.rea-tools/rea-670-motion-readiness.json. Default tshark lookup first failed
because Windows owned-process execution requires an absolute path; explicit
paths resolved it. Ghidra native function inspection used the existing focused
fallback to avoid re-importing this large target and previous timeout cost.

Upstream version and changes checked against:
https://github.com/morluto/rea/blob/main/package.json
https://github.com/morluto/rea/blob/main/CHANGELOG.md
Relevant6.6/6.7 changes include provider startup diagnostics and retained
observations; they do not themselves prove native camera control support.

## Conclusions / next gate

Observed: vendor manual movement uses a direction/speed extension command plus
neutral-on-release, unlike our absolute-position servo. Static packing agrees
with runtime USB. This is a concrete candidate transport for fluid motion.
Inference: this difference may contribute to our stepping. Native AI may also
close its loop inside firmware; this capture did not establish that behavior.

Before adding an adapter: capture low/high pan and tilt, verify speed/input
bounds, optical direction, sustained movement, neutral stop and disconnect /
cancellation behavior. Separate a host-independent motor stop/watchdog from
normal target updates, and retain standard-control fallback. Do not treat the
user's subjective native-AI smoothness as verification of this manual path.
Native selector21 activation remains gated; no attempted bypass or new write.

Windows then refused UI access: “failed to activate captured window”, followed
by “GetCursorPos failed: Access is denied (0x80070005)” on the one recovery.
Further UI inputs stopped and user asked to clear an expected prompt/unlock.
At this checkpoint vendor app remains open/network-blocked, community closed,
and OBS camera/Virtual Camera intentionally paused pending desktop access and
cleanup. Capture has ended. No further calibration needed for this investigation.

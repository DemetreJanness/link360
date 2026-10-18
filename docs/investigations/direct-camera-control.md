# Direct camera control: initial validation

Date: October 13, 2026
Target controller: 2.2.5.14
Camera firmware reported by user: v1.4.8_build2

## Question

Can LINK360 control the connected Insta360 Link without Insta360 Link
Controller running?

## Evidence and findings

REA 6.4.0 is installed locally and its CLI works. Its PE resource inspection
verified the updated executable identity. Evidence ID:
`ev_4fbf86405556a7882c9065ee49e5412b493f75a7ecaad95ae21688576d87b72d`.
The complete record is in `evidence/controller-pe-resources.json`.
This inventory does not recover camera commands.

A separate Node static string scan found Windows UVC controller names,
`WinUVCCameraController::GetKsControlInterface`, and
`WinUVCCameraExtendController::GetKsControlInterface`. The latter includes an
error string naming `QueryInterface(IKsControl)`. These are ordinary shell
inspection findings, not REA decompilation or observations of executed calls.

Using Microsoft's documented DirectShow device enumeration and IAMCameraControl
interfaces, LINK360's C# prototype successfully queried the camera directly.
The controller was closed using its normal window-close mechanism. Subsequent
process enumeration found no running `Insta360 Link Controller` process.
Final checks also found no `VirtualCameraService` process. Windows reports the
camera's service as `usbvideo`, with signed Microsoft driver `usbvideo.inf`,
version `10.0.26100.9444`. Device identifiers were not retained.

Driver-reported ranges with that app closed:

| Control | Minimum | Maximum | Step | Read status |
| --- | ---: | ---: | ---: | --- |
| Pan | -145 | 145 | 1 | Success |
| Tilt | -90 | 100 | 1 | Success |
| Roll | -100 | 100 | 1 | Success |
| Zoom | 100 | 400 | 1 | Success |
| Focus | 0 | 100 | 1 | Success |
| Exposure / Iris | — | — | — | Unsupported through this interface |

Do not interpret unsupported exposure here as absence of camera exposure
control; other property sets or vendor extensions may provide it.

The zoom test read original value 100, set 101 with manual flags, read 101 back,
and restored 100 with the original flags. A separate invocation of the manual
setter also set/read 101 and restored/read 100. Zoom 401 was rejected against
the camera's range before issuing a write.

Records: `evidence/camera-controls.json`, `evidence/zoom-test.json`,
`evidence/zoom-set.json`, `evidence/zoom-restored.json`.

Pan subsequently passed 0 → 1 → 0 and tilt passed -79 → -78 → -79,
including requested-value readback and original-value/flag restoration.
The app remained closed. Record: `evidence/motion-test.json`.

## Implementation

`src/native/CameraProbe.cs` binds the Windows camera filter directly, queries
ranges and current settings, and exposes validated manual controls.
`scripts/probe-camera.ps1` and `scripts/control-camera.ps1` provide the initial
command interface. No camera frames are captured or uploaded by these scripts.
No vendor app process is started and no vendor application DLL is explicitly
loaded. This is a driver-level control prototype, not a finished controller.

## Limits and pending work

- Driver readback is verified; visible zoom/motion has not been independently
  observed. Roll/focus writes have not been tested.
- The camera uses Microsoft's signed UVC driver. The full machine's background
  services and their network behavior have not been audited.
- OBS/Discord concurrency, reconnect behavior and control contention are untested.
- Tracking, gestures, modes and extended state have not been recovered.
- REA's initial Ghidra analysis hit its 330-second provider deadline. A subsequent
  MCP attempt hit a client deadline; the client timeout was corrected and a
  focused session restarted. That session did not produce native findings during
  this investigation and its owned process tree was stopped after direct UVC
  control was established. No recovered vendor-extension commands are claimed.

## Primary API references

- https://learn.microsoft.com/en-us/windows/win32/directshow/selecting-a-capture-device
- https://learn.microsoft.com/en-us/windows/win32/api/strmif/nn-strmif-iamcameracontrol

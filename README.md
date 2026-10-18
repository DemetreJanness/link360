# LINK360

Local Insta360 Link camera control for use with OBS and Discord.
Local Windows desktop prototype, with a production Rust/Tauri port planned.

## Reviewing this repository

Start with [the reviewer guide](docs/REVIEWER-GUIDE.md) for the code map,
build/test commands, current limitations and useful review targets.

This is an experimental Windows prototype. Physical pan/tilt/zoom and Acer
recovery have worked in user tests, but moving-subject tracking is still jerky.
Automatic empty-room setup is incomplete, and automatic restart of the Link's
built-in AI remains gated. Passing regression tests does not prove smooth motion.

Build and run the camera-independent regression suite on Windows with .NET 9:

```powershell
dotnet build src/desktop/Link360.Desktop.csproj -c Release
dotnet run --project tests/Link360.Tests.csproj -c Release
```

The default test command does not open cameras or send PTZ commands. Optional
probe/replay flags are described in the reviewer guide. Pinned local models and
voice prompts are included with their notices; the first build may restore
NuGet dependencies. REA, Wireshark, OBS and Discord are not needed for this test.

Personal settings, room maps, recordings, raw USB captures and local session
checkpoints are excluded from Git. Investigation notes retain summarized
findings; references to private local artifacts are not downloadable evidence.

## Desktop panel

With .NET SDK 9 and the Windows Desktop reference pack installed:

```powershell
./scripts/start-link360.ps1
```

The panel offers direct pan/tilt/zoom, four named local presets, previous-framing
return, cancellation, and global hotkeys. The optional OBS buttons connect only
to the authenticated companion at `127.0.0.1:4466`. HTTP proxies and redirects
are disabled. Credentials are read locally and are never saved in the repository.
Camera controls work without that companion. The OBS health check processes tiny
camera previews in memory; it never stores or uploads them. Connections stay on
loopback. There is no microphone listener, account, analytics, or telemetry code.

All shortcuts use **Ctrl + Alt + Shift**:

| Key | Action |
| --- | --- |
| 1–4 | Recall saved preset |
| Arrows | Pan/tilt by two driver units |
| Page Up / Page Down | Zoom in/out by ten driver units |
| Home | Return to previous framing |
| End | Cancel and suspend global shortcuts |
| F5 | OBS digital reaction |
| F6 | OBS save replay (requires replay buffer running) |
| F7 / F8 | OBS Acer camera / return |
| T | Toggle local torso / calibrated room tracking |

Use the panel to re-enable shortcuts after suspension. Registration conflicts
are displayed. Presets are stored in `settings.local.json` beside the executable.
All slots can be renamed and overwritten from the panel. Personal saved slots
and room calibration are not included in the repository.

Local GPU torso tracking starts off. Enable it with **Ctrl + Alt + Shift + T**
or the tracking button. The main workspace shows the fixed Acer room camera
and PTZ Link with local body-box overlays. Direct camera capture works without
OBS; when a driver refuses a second client, an optional local OBS fallback can
reuse an existing capture. Close Insta360 Link Controller before using LINK360.
DirectShow capture can fall back to an identity-matched Windows Media Foundation
source; the Acer fallback retains its negotiated format. Capture errors appear
on the room preview. The main mode selector chooses software with Acer recovery,
Link-only software or manual built-in AI; there is no separate hidden room-mode
switch. Confirm built-in AI is off on the main view before software movement.
Camera clients can contend for hardware; pause LINK360 previews before starting
OBS capture. Discord can use OBS Virtual Camera when OBS owns the video route.

Tracking adjusts pan/tilt only and preserves zoom. Two stable, unique body
observations precede movement. Missing/multiple bodies hold position; manual
controls, presets, cancel, device changes and worker failures stop tracking.
A pinned YOLOX person detector runs locally through DirectML for both cameras,
preferring the NVIDIA adapter. The Link uses a local YuNet head detector on CPU
and a GPU MediaPipe upper-body pose fallback. The small head model's ONNX CPU
path was faster than DirectML in recorded comparisons on the tested laptop.
Acer keeps the original map coordinates
so saved room points remain compatible. Adaptive filtering and a hysteresis
deadband suppress small stationary detection noise; faster motion raises the
filter cutoff and motor speed. Fine corrections are rebased in the camera worker
on its latest commanded pose, avoiding reversal against an old driver snapshot.
Each new preview frame wakes tracking; fresh Link torso tracking can continue
when Acer is temporarily unavailable. Missing both views or ambiguous subjects
holds position. No appearance
embedding, identity database, automatic recording, runtime model download or upload exists.
The same detector output also identifies chairs for blue scene-context overlays;
chairs are never tracking targets. Chair overlap alone does not prove occlusion.
Experimental shadow/contrast filters remain offline replay options because the
current comparison did not show a reliable detection improvement.

For movement analysis, enable **Save local test video (no audio)** before the
60-second test. This optional diagnostic stores the latest paired preview views
with timestamps, aim markers and control overlays in the local logs directory.
It is off by default. Videos are bounded to about 60 seconds/90 MB, with at most
three retained. No audio is captured and no footage is uploaded. Frame times can
differ between cameras; these are not synchronized exposure measurements.
New diagnostics live in `%LOCALAPPDATA%\LINK360\diagnostics`, outside this
OneDrive project folder.

You can stay visible to both cameras throughout the test. At 35–40 seconds it
temporarily ignores the Link target to exercise Acer guidance, then lets Link
tracking reacquire. Status/video mark this as a forced test, not natural loss.
Optional **Sync subject** pauses tracking and compares a centred, still torso
with the map prediction. A small session correction aligns Acer aiming with
that framing; saved points and camera home are unchanged. It clears on preview
restart. Sync does not replace geometric calibration if either camera moved.

Open **Calibrate room** for two live previews, torso/framing guides, saved-point
markers, stillness progress and a retained last failure. Confirm native tracking
is off, then choose **Start listening**. Stand still where both cameras see your
torso; no arm pose is needed. After two seconds, LINK360 centers the Link on your
torso and saves a paired position. Move to another spread-out left/right and
near/far position. A valid map needs 4–25 points. At four, choose Finish calibration or keep adding; twenty-five valid points finish automatically. Existing ten-point maps remain valid.
After completing the map, choose Test room map for a 60-second following test. Walk slowly inside the mapped area, leave the Link view while staying in the room view, then return. Stop/manual controls end the test. Last test report shows local numeric diagnostics; JSONL and summary JSON files are in the application logs directory. No images or identity data are logged, and logs stay local. Up to ten bounded test runs are retained. Reports describe observations rather than declaring physical accuracy verified.

British female voice prompts are bundled fixed audio generated offline:
“Captured tracking point,” “Please move to the next calibration point, please,”
and, on completion, “Done, thank you.” Voice prompts can be disabled or previewed.
Legacy face/body calibration points are retained separately when a new torso map
starts. Camera movement remains bounded and all point captures require fresh,
unique body observations; a brief arm-pose error no longer affects capture.

A valid saved map selects two-camera mode at startup. Fresh Link body detections guide fine centering; if the Link loses the target, the Acer guides repositioning through local interpolation between captured room points. Multiple subjects hold. Native camera AI automatic restart is not yet implemented: its original-Link toggle/state protocol must first be verified.

Room tracking holds on missing/multiple bodies and keeps looking. A stable body
inside the calibrated area resumes following. Moving either camera invalidates
the room map; reset and recalibrate after moving one. Physical room mapping and
reacquisition remain user-test gates; passing math/model tests alone does not
establish camera accuracy. The detector is anonymous and does not associate IDs
with people. Current hardware roles are Acer overview and original Insta360 Link.

For simultaneous use, let OBS capture Insta360 Link, start OBS's Virtual Camera,
and select OBS Virtual Camera in Discord. Discord's selector was inspected and
the user confirmed its live image updates. Camera global hotkeys were
tested while LINK360 was in the background via OBS/Discord window targets.

Camera commands run in a separate worker with an eight-second deadline. Failed
or cancelled work invalidates the displayed state until a fresh read succeeds.
Camera reconnect polling never replays old movement. The optional OBS watcher
reactivates the verified Link source after a device change or frozen frames,
with bounded retries; disabled or hidden sources remain under manual control.
An initial physical reconnect exposed and confirmed a frozen OBS capture;
reactivation restored live frames. A monitored physical repeat passed automatic
video/control recovery; the user confirmed Discord resumed without intervention.
See the investigation for validation limits.
Cancellation stops remaining commands; it cannot guarantee an immediate physical
motor stop once a target has reached the driver. Presets now use one worker with
eased, quantized pan/tilt/zoom targets and selectable 0.5–6 second transitions.
Live 1.4 and 3 second movements, intermediate framing and cancellation passed.
Driver units and command timing do not guarantee calibrated optical velocity.

## Direct camera controls

Requires Windows, PowerShell 7, and one connected device named `Insta360 Link`.
Close Insta360 Link Controller before changing controls.

Read current settings and driver-reported ranges:

```powershell
./scripts/probe-camera.ps1
```

Set a manual control using the driver's native units:

```powershell
./scripts/control-camera.ps1 -Control Zoom -Value 100
./scripts/control-camera.ps1 -Control Pan -Value 0
```

Available commands: Pan, Tilt, Roll, Zoom, Focus. Values must match the range
and step reported by the connected camera. Setting Focus selects manual focus.
Pan, tilt and zoom have been visually verified in OBS with the vendor app closed.
Roll/focus have only been queried. Readback is driver state, rather than optical
position measurement.

The probe can test an adjacent zoom value and restore its original state:

```powershell
./scripts/probe-camera.ps1 -TestZoom
./scripts/probe-camera.ps1 -TestPanTilt
```

These scripts use Windows COM camera interfaces and contain no network code,
telemetry, or frame capture. They do not launch the vendor application or load
its application DLLs explicitly. The connected camera uses Microsoft's signed
`usbvideo` driver. No driver replacement has been performed. The full system's
background services and network behavior have not been audited.

## Investigation

**Room scene setup** scans an empty room after a local spoken countdown. It tries
to measure the centre and four boundary targets shared by the Acer and Link,
excluding detected objects such as chairs and cups. Its separate window shows
progress, feature matches and a local object inventory/report. A failed alignment
does not become a valid scene. After a successful scan, collect person-tracking
points; the previous map is archived locally before a new one starts. Repeat setup
after moving either camera mount. The scan is a scene alignment aid, not a 3D map.

Room calibration shows saved points, measured coverage, and a gold suggested gap
for the next capture. Suggestions do not create measurements. The Tracking panel
also offers a session-only minimum body score slider with live preview scores;
lower values accept weaker detections while torso and ambiguity guards stay active.

Engineering direction and implementation gates are in
`docs/engineering-architecture.md`; they apply to the current prototype and
subsequent Rust/Tauri work.

See `docs/product-direction.md` and `docs/investigations/` for requirements,
evidence and limits. REA/Ghidra/Java are development tools under the ignored
`.rea-tools/` folder and are not needed to run the direct camera scripts.

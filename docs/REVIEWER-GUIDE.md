# LINK360 reviewer guide

LINK360 is a Windows C#/.NET 9 prototype for controlling an original Insta360
Link through installed Windows camera interfaces, with local AI and an Acer
overview camera assisting recovery. Rust/Tauri is a future production direction;
the working implementation here is WinForms and C#.

## Build and test

Use Windows and the .NET 9 SDK with Windows Desktop support. From the repository:

```powershell
dotnet build src/desktop/Link360.Desktop.csproj -c Release
dotnet run --project tests/Link360.Tests.csproj -c Release
```

The default tests use mathematical fixtures, synthetic images, pinned local
models and temporary local diagnostics. They do not open cameras, drive PTZ,
launch the vendor app or contact OBS. No GPU is required for the default suite.
Models and fixed voice prompts are bundled. NuGet restore is a build-time
dependency; the application does not download models at runtime.

`--saved-map` is a separate developer check that reads the machine's private
settings; those settings are intentionally absent in a clean clone. Other
flags such as `--scene-startup-probe` use actual cameras. Read each tool's entry
point before running optional hardware probes. `scripts/start-link360.ps1`
builds and launches the interactive application, which registers global hotkeys.

## Code map

| Area | Starting points |
| --- | --- |
| UI, actions and tracking orchestration | `src/desktop/ControllerForm.cs`, `CalibrationDialog.cs`, `SceneScanDialog.cs` |
| Windows camera control and workers | `src/native/CameraProbe.cs`, `ContinuousMotion.cs`, `SmoothMotion.cs`, `src/desktop/LocalCameraSession.cs` |
| Capture, identity and ownership | `DirectVideo.cs`, `CameraFeeds.cs`, `CameraInventory.cs`, `MediaFoundationDevices.cs` |
| Person/head/pose inference | `WholeBodyDetector.cs`, `OnnxFaceDetector.cs`, `PoseGesture.cs`, `HeadFramingAnchor.cs`, `LinkLock.cs` |
| Fine movement and Acer handoff | `LocalTracking.cs`, `FeedbackTravelBudget.cs`, `HybridTrackingPlanner.cs`, `HybridGuidance.cs`, `HandoffAlignment.cs` |
| Room map and automatic scene experiments | `RoomMesh.cs`, `RoomAlignment.cs`, `SceneRegistration.cs`, `SceneScanController.cs` |
| OBS/Discord routing | `ObsVideoRoute.cs`; Discord receives OBS Virtual Camera |
| Local test diagnostics | `RoomMapTest.cs`, `RoomTestVideo.cs`, `tests/VideoMotionReview.cs` |
| Regression entry point | `tests/Program.cs`, plus focused test files in `tests/` |

Desktop filenames without a prefix above are under `src/desktop/`.

## Useful review targets

1. **Movement quality.** Moving-subject tracking remains jerky in physical tests.
   A quiet seated hold passed, and the feedback budget reduced one observed
   oscillation, but neither establishes smooth tracking. Investigate delayed
   optical feedback, whole-unit control steps and target/source transitions.
2. **Framing and recovery.** Close standing poses can place the head outside the
   Link view. Acer uses a measured two-dimensional map; it does not reconstruct
   a 3D scene or extrapolate beyond measured coverage. Artificial takeover tests
   should be distinguished from natural target loss.
3. **Lifecycle and UI.** The controller form owns many responsibilities. Review
   cancellation, stale-state handling, UI clarity and worker boundaries before
   expanding functionality or porting to Tauri.
4. **Scene setup.** Empty-room scans have not yet completed all five anchors
   reliably. Do not treat the scene-home workflow as production-ready.
5. **Native AI control.** `NativeTracking.cs` deliberately gates activation;
   target-selection semantics are not verified. Vendor static findings are not
   authorization or evidence for speculative hardware writes.

Read [engineering architecture](engineering-architecture.md) and
[product direction](product-direction.md) alongside the implementation.
Recent evidence is in [OpenCV tracking review](investigations/opencv-tracking-review.md),
[GPU comparison](investigations/gpu-vision-benchmark.md), and
[vendor movement investigation](investigations/vendor-motion-path.md).
Historical investigation notes may describe older builds; later results take
precedence. Dependency/model notices live in `docs/licenses/`, `models/` and
`assets/audio/`.

## Privacy and review scope

Runtime inference is local. Optional OBS integration uses loopback. Recordings
require the explicit local-test-video option and remain in local AppData.
No cloud account, telemetry service or uploaded-frame pipeline is implemented.
This does not audit unrelated Windows services or vendor firmware.

The repository excludes personal camera settings, the owner's ten-point map,
private recordings, packet captures, reverse-engineering tool installations,
vendor binaries, build outputs, credentials and local assistant checkpoints.
Summarized investigation evidence remains available without those artifacts.
REA is optional development tooling, never a runtime dependency. The sibling
OBS companion project referenced by some scripts is not included; camera
controls and direct preview paths can work independently of that companion.

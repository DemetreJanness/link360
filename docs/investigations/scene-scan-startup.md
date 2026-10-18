# Scene scan startup investigation · October 17, 2026

Target: installed LINK360 WinForms prototype and original Link/Acer cameras.
Question: why did empty-room scans stop themselves or fail at the first view?

## Observations

The user reported that the two latest attempts stopped without pressing Stop.
Their local reports (16:17:29 and 16:17:56 UTC) said cancelled with no observations.
Earlier October 16 reports contained 1–6 fixed-feature matches, no accepted
anchors, 640×360 Acer images and 320×180 Link images. Their logged origins were
near tilt −80. Old reports lack the cancellation source, so they cannot prove
which Windows event caused those stops.

Source inspection found that `ControllerForm.WndProc` cancelled every action on
DBT_DEVNODES_CHANGED, even for unrelated device changes. It also found that scene
setup read the origin before starting video and reused the low-resolution Link
tracking stream. REA was not needed for these available application sources.

Read-only hardware probe, with the user closing LINK360 first:

```
LINK360.Tests.exe --scene-startup-probe <scene-fix-build/LINK360.exe>
```

Before opening video: pan −5, tilt −81, zoom 100. While both direct streams ran:
pan −8, tilt 2 then 1 then 1, zoom 100. Native tracking read false throughout.
Both streams returned 640×360 images. No motor commands, saved frames or network
services were used. This verifies changed driver readings after video startup;
it is not an optical encoder measurement or proof of scene alignment accuracy.

## Corrections

- Compare camera identities on device notifications; unrelated devices and index
  reordering do not cancel. Relevant disappearance/replacement/ambiguity cancels.
  Capture and command error paths still stop on failure.
- Open and warm both direct streams before the spoken countdown. Keep video
  alive during it, show live previews, and refresh the origin afterward.
- Use 640×360 quality-92 JPEG for scene matching. Existing tracking keeps its
  320×180 quality-80 Link contract.
- Report start/end times, phase transitions, device notifications and cancellation
  reasons locally. No device paths are included in reports.
- A failed scan no longer automatically marks camera mounts as moved. Explicit
  mount-change state and successful scene/map identity requirements remain.
- Preview restart cannot take capture away from an active scene scan. Manual
  camera controls and Stop still cancel it.

## Validation and remaining evidence

Release builds and the regression suite pass. New checks cover unrelated-device
and index changes, used/unused Acer loss, Link loss, replacement and duplicate
identity, plus separate scene/tracking capture profiles. Existing motion, mapping,
sharing/recovery policy, scene masking and registration tests pass. Synthetic UI
render at 920×720 was inspected. Live scan testing is in progress; do not yet
claim five anchors, optical home return or smooth tracking.

The installed settings already contained zero active/previous room points when
this investigation began. Their hash stayed unchanged across installation:
`3843DB6D0E1B161B86B6B0BF8591799AEB93EB3CDD50F365CC10BFF27FFE6E89`.
Two older local test logs retain the historical ten-point map. No old map has
been reactivated or mixed into a new placement.

The user raised the stopping limit to 40% remaining. Last account check: 7% used,
93% remaining. This is the displayed account allowance, not a credit balance.

## Live follow-up

Windows computer use now works via the computer-use skill (`@oai/sky`). The
agent operated the actual installed app. Accessibility reads work; element-index
clicks returned "coordinate input geometry is unavailable", so observed screenshot
coordinates and keyboard actions were used instead. No alternate UI automation.

First updated run (`scene-scan-20261017-162411-...json`) completed the countdown,
reported native tracking off, and aligned the initial scene with 100 inliers,
93.5% consistency. Origin was pan 1 / tilt -3 / zoom 100, after video warm-up.
A two-unit tilt correction was followed by a centre-outside-inlier-hull rejection.
The run stopped, announced failure, and did not publish a scene reference.

Next run (`scene-scan-20261017-162734-...json`) verified centre, then advanced
toward upper-left. Valid centres progressed (.522,.542), (.472,.472),
(.425,.389), with 88, 59, 47 inliers respectively. The next view repeatedly
returned 13-16 ratio-filtered matches. Four held-frame retries did not resolve it.
The screenshot showed a progressively higher view with masked TV/chair regions.
This is evidence of declining common feature support, not an observed timeout or
automatic device cancellation.

An ORB-to-SIFT fallback now uses the same geometric gates, including centre hull
containment. Duplicate feature positions are removed, preventing multiple SIFT
orientations from inflating the count. Synthetic known-offset SIFT and ORB tests,
mask/person/blank rejection and the full regression suite pass. The third run
(`scene-scan-20261017-163102-...json`) started at the previous unsupported pose
(pan -11, tilt 13). ORB found 1–5 matches; SIFT found 4–8. All four held checks
failed without moving. The fallback does not recover detail absent from both views.

The next build measures usable shared boundaries. Each quadrant begins at the
verified centre; feature loss can trigger a return to the last supported pose
only after fresh, person-free frames and unchanged native-off control state.
That candidate still requires two fresh optical checks and stable background.
Limited anchors must lie in the correct quadrant, span at least 0.2 in each
normalized axis collectively, and surround the centre. The UI/report explicitly
identify unmeasured edges. No interpolated anchors substitute for measurements.
Synthetic limited-coverage, wrong-quadrant, insufficient-span and duplicate-point
checks pass. A physical adaptive-boundary scan is being tested from pan 1,
tilt -1, zoom 100; do not yet claim five measured anchors.

Fourth live run `scene-scan-20261017-163802-755e47f066074f82854dfb861adc40cf.json`
verified three anchors (centre, upper-left, upper-right). Both limited upper
boundaries passed their second optical check. Lower-right stopped because a
possible person was detected at the image edge (the final preview showed a blue
shape there); no reference was published. This run demonstrates bounded boundary
revisit and partial capture, not a completed scene. It did not self-cancel.

The user subsequently confirmed neither camera mount had moved since the original
ten-point map. With LINK360 closed, the exact ten archived coordinates were
restored into its empty active map and CameraPlacementChanged was cleared. No
SceneHome was invented. The map audit passed construction; point 6 has a 14-unit
leave-one-out tilt disagreement, which is a warning, not measured aiming error.
All ten points remain intact for a recorded live tracking comparison.

The historical ten-point map was copied from the first record of the 05:02:21 UTC
October 15 local test log into `room-map-preserved-20261015-050221.json` in the
same AppData diagnostics directory. It is archived only. Current settings were
not overwritten. Map-reset actions now archive existing coordinates before
clearing; an archive/write error preserves the active in-memory map.

Room scene setup is now a visible button beneath the main preview. Scan controls
disable conflicting actions and retain readable confirmation text during scans.

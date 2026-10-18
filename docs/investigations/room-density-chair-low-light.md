# Room density, chairs and Acer low light · 2026-10-14

User asks whether virtual room points, chair detection and image enhancement
could improve lock quality/speed. Preserve ten captures and keep runtime local.

## Source and hardware evidence

Local read-only inventory: Acer Predator PH315-55; camera Acer FHD User Facing,
VID0408/PID4036, revision0003, interface00. No serial number collected. No
verified manufacturer recipe for this exact camera's low-light controls was
found. Product-family or community advice does not establish supported controls
on this installed unit. No driver/firmware download or hardware setting write.

The pinned YOLOX model/provenance already covers COCO person and chair. The
existing decoder selected only person and discarded chair. Upstream primary
source, same pinned commit:
[OpenCV Zoo demo](https://github.com/opencv/opencv_zoo/blob/47534e27c9851bb1128ccc0102f1145e27f23f98/models/object_detection_yolox/demo.py).

Microsoft documents that [auto-exposure priority/low-light compensation](https://learn.microsoft.com/en-us/windows-hardware/drivers/stream/ksproperty-cameracontrol-auto-exposure-priority)
can lower frame rate in dim scenes. This is a supported Windows mechanism, not
evidence that it is enabled or adjustable on this Acer. Longer exposure must
not be assumed to improve tracking latency. OpenCV explains
[contrast-limited histogram equalization](https://docs.opencv.org/4.13.0/d5/daf/tutorial_py_histogram_equalization.html)
and the risk of noise amplification; prettier brightness is not detector accuracy.

## Map audit

`LINK360.Tests.exe --map-audit <settings.local.json>` fits the original ten
points, evaluates a40×40-cell grid, and removes one measured point at a time
where remaining coverage permits a prediction. No settings are written.

168 virtual samples fall within the existing hull;1513 are outside and remain
unmapped. This grid samples the interpolation already available at arbitrary
coordinates. It adds neither depth nor new coverage. Virtual values must not
be labelled new measured points or used as extra independent observations.

Leave-one-out disagreement in driver units:

| Saved point | Pan | Tilt |
| --- | ---: | ---: |
| 1 | 2 | 3 |
| 6 | 6 | 14 |
| 10 | 1 | 2 |

Other points could not be cross-validated within the remaining hull/fit guards.
Point6 merits a repeat observation; disagreement could reflect a bad capture,
nonlinear/parallax effects or changing body-box anchors. It is not proof that
point6 is wrong. No point was deleted, overwritten, reweighted or extrapolated.

## Chair context

`WholeBodyDetector.cs` retains class56 chairs with the same inference output,
confidence cutoff and per-class NMS; person scoring/coordinates are unchanged.
`BodyResult.Chairs` carries scene boxes; preview uses thin blue chair overlays.
Chairs never become motor targets, and people are not discarded simply because
their boxes overlap chairs. Overlap cannot establish front/behind ordering.
This adds observable chair context, not a validated occlusion solution.

## Offline enhancement experiment

`LINK360.Tests.exe --room-quality <acer.mp4>` compares identical sampled frames
using raw, gamma0.8 shadow lift and LAB luminance CLAHE(clip2,8×8 tiles). Both
enhancements are replay-only options. Production still uses raw input. No
temporal averaging, extra model pass or camera exposure change is introduced.

52 frames at2Hz from the user-supplied Acer preview recording:

| Processing | One person | No person | Multiple | Mean pipeline |
| --- | ---: | ---: | ---: | ---: |
| Raw | 15 | 37 | 0 | 8.78 ms |
| Shadow lift | 15 | 37 | 0 | 8.79 ms |
| Local contrast | 16 | 35 | 1 | 9.75 ms |

Chair detections were zero in all three runs. There is no labelled ground truth;
the recording is282×216 and contains UI overlays/compression. These results
cannot establish true recall, false positives, live lock time or sensor quality.
They do not justify switching filters on. Need a raw paired diagnostic clip
covering an empty chair, person in front/behind it and the actual dim lighting.

Person-regression replay of61 PTZ frames retains identical anchor coordinates,
61 usable torsos and zero quiet corrections. Runtime pipeline measured18.15ms
mean in this run; load differs from prior runs, so no speedup is claimed from
chair decoding. Full saved-map/motion/handoff/alignment suites pass; build has
zero warnings/errors. Existing model license/provenance is retained.

REA was not needed: model source, SDK documentation and deterministic offline
replays supply evidence without investigating a new proprietary protocol.

# Head and shoulders as the primary Link anchor

2026-10-14. User clarified that tracking should primarily follow head and
shoulders; a full torso is not required. Investigation inspected PoseGesture.cs,
LinkLock.cs, the local inference worker and fine-tracking planner.

Previously, shoulder-based fallback still projected its anchor down the chest,
and the Link lock required a person box spanning at least 35% of frame height.
The existing hip plausibility guard rejected some unreliable seated pose outputs.

The Link pose worker now prefers a visible nose plus both shoulders, with an
anchor between head and shoulder midpoint. Hips are optional for this path.
Geometric checks require shoulders below the head and sufficiently far below the
person-box top, with plausible separation and all three landmarks inside the
detected person. Upper-body locks accept boxes from 20% frame height. The existing
torso path remains available when a reliable head/shoulder trio is unavailable;
its hip guard and height requirement are unchanged. Acer room coordinates and
all saved pan/tilt correspondences retain their original definition.

This is geometric plausibility, not identity verification or independently
verified anatomy. A high-confidence pose model can still be wrong. The live
preview identifies the selected head/shoulders lock. Shot zoom remains separate.
No new models, network access, recording or telemetry are introduced.

Validation includes seated head/shoulders without hips, partial-height lock,
face-like shoulder placement rejection, missing-shoulder rejection and existing
tracking/map regressions. Live detection quality and camera smoothness require
another physical test; synthetic fixtures do not establish those outcomes.

Release build and regressions pass. Final settings inspection found eight newly
captured points (file timestamp 22:27 local), different from the earlier ten-point
map. This change did not write settings; the current eight-point map also passes
exact saved-position reproduction. No older map was restored over this file.

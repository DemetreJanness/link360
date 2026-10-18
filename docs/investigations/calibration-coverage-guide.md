# Calibration coverage and live score controls

Target: LINK360 WinForms calibration and local whole-body tracking, 2026-10-14.

The Acer calibration preview now outlines the convex hull of measured image-space
positions. A dashed gold ring identifies the largest sampled gap inside that hull,
at least .07 normalized units from existing measurements. Geometry is recalculated
only when measurements change. Guidance stops at 25 points. Sparse or collinear
captures request spread-out positions instead of claiming coverage.

These are collection suggestions, not synthetic calibration points, a physical
point cloud, or a guarantee of clear floor, visibility from both cameras, or motor
reachability. Nothing writes or replaces saved room points. Additional captures
near coverage edges are still needed to expand the measured area.

A session-only minimum body score slider spans .35–.80, default .50. Each local
body-worker request carries the threshold; legacy base64-only requests still work.
YOLOX person filtering and software body guidance use the chosen threshold. Chair
filtering, torso geometry, pose plausibility, ambiguity, freshness and motion guards
remain unchanged. Session sync retains its .50 room-score requirement. The separate
legacy face detector is unaffected. Preview captions show retained detection scores
and threshold; missing/below-threshold candidates have no displayed numerical score.
Model scores are not calibrated probabilities of a correct identity or lock.

Validation: Release builds and the saved-map regression suite pass, including gap
selection, coverage containment, degeneracy, point capacity, unchanged measurements,
default/lenient confidence and missing/ambiguous torso guards. A synthetic Acer
preview was rendered at 510×300 and visually checked for the guide and live score.
The settings SHA256 remained
1A9AEEDA4E1382BDD8164259BF06247F71BE469B5541CA54645B320971F95828
with all ten points. No live accuracy or smoothness improvement is claimed from
these offline checks. LINK360 remains closed after installation; all work is local.

# Live test analysis and paired diagnostic video · 2026-10-14

Question: correlate observed jerkiness with local frames, inference and camera
commands. User reports following/recovery seems to work but motion remains jerky,
and explicitly requests frame review during tests. No native protocol changes.

## Completed numeric test

File `room-test-20261015-034624-5a0972af.jsonl` (UTC filename), run at
20:46–20:47 America/Los_Angeles on October 14. 60.020 seconds completed.
751 logged samples, 616 movement requests: 416 relative Link corrections and
200 absolute Acer mesh requests. Fourteen transitions between these request
sources; 20 pan and 29 tilt requested-direction reversals. These are requests,
not measured motor reversals. 232 samples had no usable pose torso anchor.
Median frame-read age: Acer 62.76 ms, Link 64.80 ms. No samples crossed the
600-ms stale cutoff. These ages exclude exposure/driver buffer/motor latency.
71 samples were outside the calibrated hull and held.

`scripts/analyze-room-test.mjs <jsonl>` reproduces counts and phase summaries.
Its 0–15/15–35/35–60-second phases follow the instructions given to the user,
not measured activity labels. Room coordinates varied within these windows;
do not claim the initial interval establishes stationary jitter objectively.

Inference: handoffs/anchor loss and changing targets merit examination alongside
frames. The log alone cannot show whether each reversal was appropriate, nor
prove that high request rate caused the visible jerk. Previous test saved no
frames; no retrospective visual diagnosis is possible from it.

## Opt-in paired video

`RoomTestVideo.cs`: test-only MJPEG AVI, 960×360, nominal 30 FPS, paired latest
camera views, aim markers, read timestamps, controller source, error, speed,
resolved/issued targets and driver readings. No audio or network code. Source
frames are not exposure-synchronized; timestamps expose camera skew. Numeric
driver/issued positions are not motor encoders.

The checkbox is off by default. Encoding runs in a separate task with only one
replaceable pending packet; it cannot accumulate a queue of old images. Clip
bounded to 62 seconds / approximately 90 MB, three clips retained. Test reports
carry the video path and any recording error. Data used for overlay is captured
with the decision before awaiting the camera worker, avoiding substitution of
a newer detection after the command has already been selected.

New diagnostics target `%LOCALAPPDATA%/LINK360/diagnostics`, outside the OneDrive
project. Earlier staged build used the application logs folder; no diagnostic
AVI had been created there when the storage issue was caught. The corrected
build requires closing/reopening the running app. Old numeric reports remain
readable; room points/settings are not migrated or changed.

## Verification

Release and test builds pass with zero warnings/errors. Synthetic paired-video
test finalized and decoded 49 frames at 960×360 without opening a camera;
an extracted full frame was visually inspected for readable overlays. Existing
motion/map/override/reconnect tests pass, including every saved ten-point pose.
Test footage and analysis outputs stay ignored; no identifiable footage is
committed. Full live video correlation remains pending the next recorded test.

Recommended next test: still 15 s; slow then fast sideways movement until 35 s;
then leave Link view while visible to Acer and return. Inspect frames around
source changes and direction reversals before changing the control algorithm.

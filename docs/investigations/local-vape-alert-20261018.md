# Local VAPE ALERT lab

Date: 2026-10-18. Community branch only. Target: existing local OBS websocket
and camera source; no new proprietary camera commands or runtime cloud services.

## Implementation and evidence

Static inspection: LocalObsRpc connects only to 127.0.0.1, disables proxy use,
and reads OBS credentials locally without logging them. The separate alert form
reads 640x360 JPEGs of the existing Link OBS source at up to five polls/second;
it does not open hardware capture or call camera controls. Sharing has a launch
button; --vape-alert starts only the lab, without ControllerForm/map initialization.
Tracking code is unchanged except for the Sharing launch button.

VapeDetector uses existing OpenCV dependencies for an explicitly selected,
in-memory grayscale template and a pale positive-change plume cue. YuNet uses
the existing checksum-verified local 320x180 model to restrict plume analysis to
space beside one face, excluding the face and clothing below it. This is neither
a trained vape classifier nor proof of smoke chemistry. Template matching has
limited scale/rotation tolerance and includes whatever background is selected.

VapeEventPolicy requires a two-second quiet baseline, a recent reference match
near the mouth, removal for 600ms, then a sustained visual plume cue. It resets
after events, missing/ambiguous faces, stale frames, errors and Stop. Cooldown
is 10–120 seconds (default 20). Automatic mode is off at startup. The manual
button remains available. No frames, templates, video or audio are recorded.

VapeObsAlert adds only two dedicated local sources to the current program scene:
a GDI text overlay and a local WAV media source. It sets local voice monitoring
and output, presents for five seconds, and hides both on completion/cancellation.
No scene switching, replay, streaming start or camera/motor writes occur. Scene
item IDs are cleared before preparing another scene. No browser source is used.

Primary references inspected:
- https://docs.opencv.org/4.13.0/d4/dc6/tutorial_py_template_matching.html
- https://github.com/obsproject/obs-websocket/blob/master/docs/generated/protocol.md
REA adds no necessary evidence for these already exposed source/API contracts.
No claim of a new REA readiness check or runtime capture is made.

## Checks and live results

Initial guarded run 20261018-160957-dde67274 passed; sequence-guarded run
20261018-161518-3dc0a939 passed. Final build/default fixtures passed after stale
sequence reset and scene-item cleanup isolation. Fixtures cover stationary input,
synthetic pale change, global exposure rejection, stale gaps, low-detail template
rejection, actual template matching, cooldown and the pre-exhale sequence guard.
Synthetic fixtures establish algorithm behavior only, not real classification.

Manual OBS alert appeared in the shared video. Initial audio was output-only;
after enabling monitoring on the dedicated voice source, the user confirmed
"Holding; voice heard". RPC independently verified both dedicated sources hidden
after the five-second effect, monitoring-and-output enabled, Virtual Camera active.

First automatic live attempt failed: user reported it fired before exhaling.
Grey clothing and handling motion were observed to produce pale-change cues.
Automatic mode was stopped; this prompted the face region and temporal sequence
guards. Final live acceptance is recorded below after its result is known.

## Limits

Steam, lighting, pale objects and handling can resemble vapour. Rotation/scale or
hand occlusion can break a template. This prototype is not a safety smoke alarm.
Concurrent tracking performance was not physically benchmarked during this task;
the existing tracking regression fixtures passed, with tracking policy and saved
maps unchanged. No private frame/template or credentials belong in Git.

Final live test: the sequence guard reached a quiet baseline with zero events,
then produced one completed alert during the prompted trial. The user still
reported timing failure ("No—early, late, or missed"). It is NOT accepted as
reliable automatic detection. Stop was applied, automatic mode verified off,
and both OBS alert sources were verified hidden. The shipped lab explicitly
states automatic timing failed live validation; manual trigger is the usable
feature. Further automatic work requires representative labelled examples and
better temporal/object modelling, not another sensitivity-only threshold change.

Final verification: protected primary exe/dll/settings, live community settings,
and archived ten-point map matched their SHA256 baselines. OBS camera input and
Virtual Camera were active, both owned alert scene items hidden. Focused detector
and fake-OBS fixtures passed after the final close guard, which delays closing
until in-flight reads/effects finish and bounded overlay cleanup is attempted.
Manual alert and voice are physically/user verified; automatic timing is failed.

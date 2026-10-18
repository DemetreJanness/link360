# Optional local OBS vape alert (proposed)

User request, 2026-10-18: track a vape and detect visible smoke/vapour, then
announce "VAPE ALERT!" in OBS. This is a separate experiment, not implemented.

Use the existing shared preview frames locally. Keep object tracking and visible
plume detection as separate signals: a hand-held vape may be occluded, and visible
plumes can resemble glare, steam or scene motion. Show each signal and provide a
manual trigger during validation before enabling automatic OBS actions.

An opt-in event should show the OBS alert and optionally play a local voice clip,
with a configurable cooldown and mute toggle. It must not change the accepted
PTZ speed/handoff policy. No uploads, telemetry or background recording; any
training examples require explicit local capture. Do not describe this as a
smoke alarm or verified vape classifier. Select an actual detector only after
reviewing local hardware, model licensing and representative false positives.

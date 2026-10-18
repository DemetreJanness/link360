# Visible movement and desktop controls

Date: October 13, 2026 (America/Los_Angeles)

## Physical verification

Insta360 Link Controller remained closed. OBS's source
`Main Camera - SELECT INSTA360 LINK` supplied local preview images. Initial
driver state was pan 7, tilt 4, zoom 118.

- Pan 7 → 22 visibly shifted the room's fixed wall/bed landmarks horizontally.
- Tilt 4 → 14 visibly shifted the frame upward toward the ceiling.
- Zoom 118 → 168 visibly enlarged the same fixed wall landmarks.
- Each axis was restored in a finally block. A final preview showed restored
  framing. Later stable zoom readback was 117; this small drift/quantization
  requires further investigation, rather than claiming exact optical position.

These observations establish visible response, not calibrated motion angles,
settling time, deterministic latency, or synchronized smooth motion. Preview
images were temporary ignored development artifacts and are excluded from the
repository; no runtime frame capture is part of the desktop application.

## Implemented prototype

The .NET 9 Windows Forms panel preserves the existing C# UVC adapter. A separate
worker processes one-line JSON requests and returns complete JSON responses.
The UI serializes operations, bounds each worker to eight seconds, kills a
timed-out/cancelled worker, invalidates stale state, and polls for fresh reads.
It does not automatically replay movements after rediscovery. A single-instance
mutex prevents duplicate desktop controllers.

The panel has numeric controls, nudges, four named presets, previous-framing
return, global shortcuts, cancellation/suspension, and optional authenticated
OBS companion buttons. Preset poses are fully range-validated before the first
axis write. Settings use a temporary file plus atomic replacement and stay local.
The production Rust/Tauri direction remains unchanged; this is a tested interim
desktop prototype, not a completed production architecture.

## Validation completed

- Release build: zero errors and zero warnings.
- Live compiled-worker read protocol and invalid/unknown-control rejection.
- Invalid commands left zoom unchanged.
- Main preset saved, persisted, and appeared after restarting the app.
- UI zoom button changed 117 → 127; Close-up preset saved locally at 127.
- Global pan shortcut moved 7 → 9 with LINK360 in the background, using an OBS
  window-targeted keyboard injection.
- Global preset-1 shortcut restored pan 7 / tilt 4 / zoom 117 using a Discord
  window-targeted keyboard injection. Foreground activation of those existing
  windows was unreliable in the helper, so exact foreground-app focus is not
  claimed. Background global-hotkey handling is verified.
- End shortcut suspended controls. Subsequent pan shortcut left pan at 7.
- UI suspension toggle re-enabled controls after that test.
- Preset 2 recalled zoom 127; Home restored the previous zoom 117, with pan 7
  and tilt 4 unchanged. An initial failed recall exposed contention with periodic
  reads; explicit controls now wait for background reads (latest request only)
  and are discarded on cancellation. The repaired live recall/return tests passed.
- OBS authenticated status endpoint worked; requests without a token returned
  401. OBS was not streaming. Virtual Camera was not active at the check.
- LINK360's local OBS return button completed successfully; the panel displayed
  `OBS: return` after the companion returned an HTTP success response. Camera
  framing remained separate from that digital scene action.

## Pending validation and limits

Discord's local video preview was not verified: desktop-helper input geometry
was unavailable for its existing window despite refreshed selection/activation.
No Discord camera broadcast or call-state change was made. OBS captured live
video throughout the physical-control tests; simultaneous Discord video remains
unverified. Use OBS Virtual Camera as a candidate shared-output route, then test.

Unplug/replug, two identical cameras, hung native driver, partial preset failures,
physical stop timing and latency under game load remain untested. Roll/focus
motion and vendor-specific tracking/modes remain outside this verified baseline.
No cloud voice recognition or AI tracking was added.

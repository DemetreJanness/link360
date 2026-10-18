# Acer capture and tracking-mode UI · 2026-10-14

Target: current LINK360 .NET desktop, original Link and Acer FHD User Facing.
User returned for live stationary/moving/recovery tests, but Acer preview failed
to restart. Preserve ten room points and local-only operation.

## Observations

- Windows PnP reported both cameras OK. Vendor controller and OBS were closed.
  Link preview worked. Native tracking read returned false, without writes.
- Acer's DirectShow capture failed before producing a frame. It failed again
  after all LINK360 processes were closed. OpenCV stderr said the backend could
  not capture by index. This does not establish that another app owns Acer;
  the former error text overclaimed that cause. User reports no other Acer client.
- Media Foundation opened the matching camera but produced no samples after
  separate width/height/FPS changes. Keeping its negotiated default format
  produced three fresh packets: sequences 1,3,4; read timestamps advanced;
  JPEG sizes 21319,20280,19704 bytes. No frame was written to disk or uploaded.
  The format-renegotiation explanation is inferred from this comparison, not a
  driver-internal trace.
- User screenshots show page titles below content, hidden room-mode checkbox
  unchecked while main mode says Software + Acer recovery, and paused Acer
  surface despite a capture failure. These are distinct from tracking quality.
- Computer-use helper repeatedly returned `foreground window did not report a
  process id`, including after user foregrounded LINK360. User closed the app
  for installation; no alternative UI automation was used.

## Changes

`MediaFoundationDevices.cs` enumerates video-only MF sources using installed
Windows SDK ABI/GUIDs (`mfidl.h`, `mfobjects.h`, 10.0.22621.0). It matches the
same name and physical device-instance path as DirectShow, stripping only the
DirectShow moniker prefix and backend-specific trailing interface category.
It never selects another camera by an assumed shared index. Rechecks identity
after opening. No PnP restart or security/privacy changes were needed.

OpenCV's [Media Foundation source](https://github.com/opencv/opencv/blob/4.x/modules/videoio/src/cap_msmf.cpp)
uses MFEnumDeviceSources and its returned index. It was inspected to establish
the backend's enumeration contract; no implementation was copied into LINK360.
REA adds no useful evidence for this source/SDK-level fault, so no new REA
provider call was needed. Existing native-protocol investigation is separate.

`DirectVideo.cs` retains working DShow capture for Link; fallback opens the
identity-matched MF source and keeps its native format, resizing after Read.
CameraFrames no longer hides direct Acer failure behind an OBS connection
attempt when OBS is closed. Acer failure is shown on its preview, and usable
primary tracking can continue without stopping on a room-preview exception.

`ControllerForm.cs`: corrected page docking order; one mode selector controls
actual software+Acer, Link-only or manual-native mode. Removed duplicate hidden
room-mode setting. Built-in-off confirmation is on the main view. Start tracking
starts missing room/Link previews, uses Start/Pause labels, and reports startup
errors. Main preview toolbar includes the 60-second numeric test action.

## Validation and outstanding work

Release build passes with zero warnings/errors. Existing motion/handoff/map
fixtures pass, including reproduction of all ten saved positions. Settings
SHA256 unchanged across installation:
`4C6A079EC2A9A9B986C85FFEA034E54E65D7847527A94412559331A57B7481D3`.
Camera-identity normalization fixtures cover different interface categories
and separate physical instances. Hardware probe establishes capture freshness,
not preview usability or motor smoothness. Updated live test is requested;
record its result before claiming stillness, following or Acer recovery passed.
Native activation remains gated. No images/identity data are added to reports.

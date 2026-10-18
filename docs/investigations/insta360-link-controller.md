# Insta360 Link Controller: initial inventory

Date: October 13, 2026
Project: LINK360

## Target identity

### Updated baseline after user update

- Controller UI version (user screenshot): `v2.2.5_build14`
- Executable file/product version (rechecked): `2.2.5.14`
- Executable SHA-256: `28B9242B275ABC8E4A4D11C2997919649EAEDDC59E5889EC189DC2A031D8F942`
- Camera firmware (user screenshot): `v1.4.8_build2`
- The screenshot shows “Help us improve your user experience” checked.
  This establishes the displayed setting only; its network behavior has not
  been investigated. Device identifiers are intentionally omitted.

The inventory below predates the update; component presence must be rechecked
before relying on it for analysis of the updated executable.

### Previous baseline

- Executable: `C:\Program Files\Insta360 Link Controller\Insta360 Link Controller.exe`
- File/product version: `2.2.4.14`
- Product: Insta360 Link Controller
- Company: Arashi Vision Inc.
- Executable SHA-256: `326488F7D32FF22990A29BE96D3785DA17D39B56DF88874A8469FF83FE99B2B8`
- Product reference: https://www.insta360.com/product/insta360-link

Identity was read using PowerShell FileVersionInfo and Get-FileHash.

## Observed installation artifacts

The installation contains Qt6Core, Qt6Gui, Qt6Qml, Qt6Quick, Qt6Widgets,
Qt6Multimedia and Qt6WebSockets DLLs; qml and platforms directories;
OpenCV 4.7 DLLs; ONNX Runtime, DirectML, MNN and TensorRT libraries;
VirtualCameraService.exe, InstallVirtualCamera.exe and VirtualCamClient;
and a LibusbKDriverInstaller directory. Resources/background contains JPEG
backgrounds, thumbnails and MP4 files.

These are installation inventory observations. DLL presence does not establish
which libraries are loaded or which component implements a particular feature.

## Initial interpretation

The artifacts suggest a native Windows application using Qt/QML, with video,
inference, and virtual-camera components. Native PE analysis is the appropriate
starting route; JavaScript/Electron analysis is not justified by this inventory.
The USB installer directory suggests investigating device integration, but
does not establish the camera's actual command transport or packet format.

## Product-level behavior

The official product page describes manual brightness, exposure and gimbal
controls in the desktop controller, plus camera tracking, zoom, gesture control,
and DeskView, Whiteboard, Overhead and Portrait modes. It lists UVC 1.1 and
UAC 1.0. These are advertised product capabilities, not runtime verification
of this installed controller or evidence of vendor command IDs.

## Implications for LINK360

Candidate investigation sequence, pending the user's desired feature:

1. Device discovery and manual gimbal/zoom control.
2. Image controls and how standard controls differ from vendor extensions.
3. Tracking/mode state and presets.
4. Virtual-camera integration, if required by LINK360.

Keep device transport behind an adapter with explicit capability reporting.
Add command implementations only when their format, ranges and behavior are
supported by evidence. Model and background files do not justify copying assets
or assuming that host-side inference is required for LINK360.

## Readiness and limitations

REA has not been installed or run. Node.js v24.19.0 is available; npm is absent
from PATH and was not found beside that Node executable. No rea, ghidraRun or
objdump command was found on PATH. Tools may exist elsewhere.

The controller was not launched, camera hardware was not accessed, no USB
traffic was captured, and no executable was decompiled. This record establishes
the target and analysis route, not a recovered control API.

Next prerequisite: select the first behavior LINK360 should reproduce, then
prepare REA's native-analysis provider and inspect the relevant code paths.

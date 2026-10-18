# Local vision acceleration · 2026-10-17

## Question and environment

The user asked whether both cameras use the GPU and authorized moving more
work there if it improves latency. Existing live worker command lines confirmed
`--body-worker --gpu --whole-body` for Acer and the same flags plus `--torso`
for Link. Source selects the NVIDIA adapter through DirectML. Windows reports
an RTX 3070 Ti Laptop GPU and Intel Iris Xe; no driver or package changed.
Main person detection and pose fallback already use DirectML. This is provider
configuration/runtime worker evidence, not a full per-operator GPU profile.

The remaining small head model used OpenCV DNN on CPU. OpenCvSharp 4.13 and
ONNX Runtime DirectML 1.24.4 were already installed. Reference contracts:

- [DirectML provider](https://onnxruntime.ai/docs/execution-providers/DirectML-ExecutionProvider.html)
- [OpenCV 4.13 FaceDetectorYN](https://github.com/opencv/opencv/blob/4.13.0/modules/objdetect/src/face_detect.cpp)
- [ONNX schema](https://github.com/onnx/onnx/blob/main/onnx/onnx.proto)

Source and recorded replay were sufficient for this task; REA was not needed
for the GPU comparison. It was used separately for vendor movement analysis.

## Reproduction and evidence

`node scripts/prepare-yunet-benchmark.mjs` derives a benchmark model from the
checksum-pinned local YuNet model. It changes input/output shape metadata to
320x192 and removes stale optional intermediate shape annotations. All graph
nodes and weights are checked byte-identical. Preprocessing matches OpenCV:
320x180 BGR float32, zero padding at bottom, no normalization.

Initial experiment retained stale intermediate annotations and failed DirectML
execution with a device-instance/invalid-call error. It was never deployed.
After removing those annotations, ONNX CPU was validated first, then DirectML
completed without that error. Both existing live previews remained working.

`LINK360.Tests.exe --gpu-head-benchmark <paired-avi> <derived-model> [--cpu]`
replays 240 quarter-second samples, alternates backend order, and excludes the
first eight timings. Measurements include JPEG decode, input preparation,
inference and output decoding/NMS; they exclude capture and motor response.
The two live previews were running during the initial comparisons. All private
recordings remain in the user's local AppData diagnostics directory.

| Recording suffix / backend | OpenCV CPU mean / P95 ms | Candidate mean / P95 ms | Count mismatches |
| --- | --- | --- | --- |
| 165922 / ONNX CPU | 4.86 / 5.78 | 2.68 / 3.64 | 0 / 240 |
| 165922 / DirectML | 4.21 / 5.57 | 5.05 / 8.07 | 0 / 240 |
| 165154 / ONNX CPU | 4.71 / 5.55 | 2.69 / 3.76 | 0 / 240 |
| 165621 / ONNX CPU | 4.29 / 5.27 | 2.65 / 3.44 | 0 / 240 |

Across the 720 distinct CPU comparison samples, maximum box-coordinate
difference was below .000044 pixels and confidence difference below .00000031.
These are backend equivalence checks, not anatomical ground truth. A final
run of the promoted class during preview startup measured 7.28 ms versus
4.07 ms mean, still zero mismatches: load affects absolute timing.

## Installed decision and contracts

`LocalOnnxFaceDetector` is the default head backend on CPU. It is faster than
both the old CPU path and this DirectML trial under the measured load. Main
body/pose detection remains on DirectML. The GPU head option is accessible
only through the offline benchmark, not a user setting or default runtime.
Capture coordination, CPU image preparation and the motor loop are unchanged.

Derived model SHA256:
`7c588045dc2c5fadfca1e52a97097f1bdf188c9c5d2370746c089fb0907f4f2b`.
Reproduce the packaged model with `--runtime-model`; runtime checks its exact
hash. Model notices and provenance remain under models/, and the adapted
decoder's OpenCV license is retained under docs/licenses/.

FaceResult coordinates, threshold .85, integer-box NMS .3 and top-100 limit
are preserved. Head/body geometry and multi-person ambiguity gates are intact.
No telemetry, accounts, network inference, frame uploads or camera command
changes were added. No camera firmware or Windows driver changes.

Validation: Release and test builds succeed without warnings; full regression
suite, blank-image/wrong-dimension guards, saved-map reproduction and recorded
backend comparisons pass. Installed live previews showed Acer detection and
Link head framing with tracking off. All ten original room points compare
exactly equal to the preserved archive. Physical smoothness is still unresolved.

# Yap direct camera frame experiment — 1.3.90

## Decision being tested

The 1.3.89 iPhone trace used `WorkerWebgpu`, but canvas-to-VideoFrame preparation still took roughly 21–30 ms at 1080p. GPU command submission was small; it did not measure GPU execution. Heat felt unchanged. Test whether the original camera frame can survive encode/decode with correct orientation before replacing preparation in normal calls.

## Implementation

Open `/diagnostics/frames`, also linked from Settings → Help and diagnostics → Diagnostics. This is a local camera/encoder/decoder loop with an explicit choice between direct camera frames and the current WebGPU painter. It retains the worker-to-page frame transfer boundary. Direct mode never allocates a canvas frame or probes WebGPU. Reference mode never silently changes to direct or WebGL.

The test requests H.264, 5 Mbps, hardware preference, and 1080p/30 by default. Settings also offer 720p, 60 fps and the rear camera. Actual camera, raw, encoder and decoded dimensions are reported independently; a request is not proof the camera delivered that mode. Both previews use contain sizing without CSS mirroring. Hardware preference does not confirm hardware selection.

Each run lasts 30 seconds and stops on hiding, navigation or a call starting. All frames and both track clones are released. Encoder/decoder queues are bounded; a skipped encoded picture forces a fresh keyframe. Media never leaves the device. Optional numeric sharing uses the existing authenticated, operator-gated, 15-minute diagnostics sessions, with the same bounded uploads and one-hour trace retention. `FrameExperiment=1` is direct, `2` is WebGPU; `CallId` identifies a local experiment run, not a call. Downloads contain numeric samples and the codec label only.

## Real-device procedure (pending)

1. End calls, open the experiment on iPhone, and enable numeric sharing.
2. Keep lighting and phone position stable. Run direct 1080p/30 for 30 seconds, then WebGPU at the same settings. Repeat in reverse order to limit warm-up bias.
3. Hold readable text in view. Confirm the decoded view matches the camera for front/rear cameras, portrait, landscape, and rotating during capture. Report any rotation, mirroring, cropping, stretching or frozen output. Numeric dimensions alone cannot prove orientation is correct.
4. Compare actual dimensions, capture/encoded/rendered fps, preparation average/p95, snapshot time, codec callback delay and queue/drop counts. Treat the first configuration/startup sample separately. Do not compare runs where the camera supplied different sizes or fps.
5. Try 1080p/60 only after 1080p/30 passes. A local loop is not evidence that a bidirectional call sustains 60 fps or has acceptable heat.

## Follow-up order

- If raw frames preserve orientation, integrate the proven path with authenticated orientation signaling and capability negotiation; test real bidirectional calls.
- If metadata is hidden or the decoded image is wrong, keep the experiment explicit. Do not guess orientation from aspect ratio or remove working production preparation.
- Next investigate congestion decisions with recorded local processing, receiver delay, expiry and network evidence; avoid treating intentional stale-frame expiry as repeated network loss.
- Then compare incoming rendering and local preview costs under the same capture load. Keep encryption and real transport in the sustained call benchmark.
- If browser frame preparation remains too expensive, compare a WebRTC media prototype with an explicit E2EE design before considering a native rewrite.

## Validation

Worker tests cover original-frame identity, hidden metadata, bounded ownership, cleanup and explicit unavailable-WebGPU failure. Main-loop lifecycle test covers permission resolving after cancellation. The browser harness `scripts/yap/frame-experiment-check.html` uses a synthetic source with the actual worker, WebGPU, H.264 encoder and decoder; it cannot validate Safari camera orientation or thermal performance. Diagnostics schema tests preserve unknown orientation and distinct raw/decoded sizes. Real iPhone orientation and performance results remain pending.

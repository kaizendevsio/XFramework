# Safari direct frames and receiver orientation — 1.3.91

The 2026-10-20 iPhone experiment delivered approximately 30 fps with direct sensor frames, with median preparation around 0.02–0.03 ms. The WebGPU reference spent roughly 23.7 ms preparing each frame, primarily in the canvas snapshot. Both pictures were sideways; the reference also stretched landscape pixels into portrait dimensions. These elapsed measurements do not establish CPU utilization or thermal improvement.

## Implementation

- The production capture worker transfers the original frame. It no longer imports a GPU painter, uploads textures, or creates a canvas-derived frame.
- Browsers exposing rotation/flip retain the existing metadata neutralization path. Safari with missing metadata compares unrotated `copyTo` pixels with a 24×24 native video preview. Two confident matches establish one of eight rotation/mirror transforms. Camera switches, source/display dimension changes, and screen rotation invalidate the result.
- Calibration performs temporary pixel reads locally; once resolved, steady-state capture does no pixel read or redraw. Calibration requires a sufficiently detailed scene. Ambiguous images time out explicitly after ten seconds rather than guessing a rotation or silently returning to WebGPU/WebGL.
- The existing authenticated orientation header travels with the encoded picture. The receiver applies it in its existing draw operation, swapping canvas dimensions for quarter turns. There is no sender canvas snapshot.
- Hidden-metadata capture requires all peers to support the already-established orientation capability. Older peers get an update notice. Capture failures release camera tracks and leave the voice call available.
- The local `/diagnostics/frames` direct experiment uses the same calibration and receiver transform, shows the selected orientation code, and remains available for front/rear camera verification. The historical WebGPU comparison is deliberately retained only in the experiment.

## Validation

- 178 JavaScript tests pass, including all eight transforms, NV12/RGBA stride handling, ambiguity rejection, zero steady-state reads, invalidation, old-peer rejection, and stopping during calibration.
- Chromium WebCodecs round trip: eight rotation/mirror fixtures, independently constructed reference images; all pass dimension and decoded pixel comparisons (mean error 2.80/255 after H.264). Harness: `scripts/yap/direct-orientation-check.html`.
- Three repository browser orientation integration tests pass. The production worker also passes with WebGPU disabled: eight metadata fixtures, ten-frame H.264 round trip, and synthetic delivery at 30.15/60.13 fps. This measures worker transfer, not sustained phone encoding or thermals.
- 40 video-call/live-diagnostics client tests and 18 Bolt orientation tests pass.
- Yap client builds with zero errors and existing warnings.

## Device acceptance still required

Synthetic fixtures cover front/rear mirror conventions, but do not substitute for the physical iPhone sensor and Safari preview. After deployment, use a detailed scene or readable text, test front and rear cameras at 1080p/30, and rotate through portrait and both landscape directions. Confirm the remote picture matches the unmirrored native preview without cropping or stretching. Include camera switching and a fresh call. Test orientation lock separately.

Diagnostics should show `worker-processor`, `verified against preview; receiver applies …`, and no ongoing canvas preparation/snapshot cost. Sustained heat and 1080p/60 remain separate real-device measurements, not claims established by these tests.

## Live acceptance and recovery follow-up — 20 October 2026

The 1.3.91 iPhone/Android call confirmed `WorkerProcessor` on Safari, steady preparation below 0.1 ms, and sustained capture near 30 fps at 1080p. The user confirmed upright, unstretched front/rear views through portrait/landscape changes and reported the iPhone felt a bit cooler. This is subjective thermal feedback, not a temperature or power measurement. Orientation lock and sustained 60 fps remain unverified.

Separate failures remain in the trace: abrupt budget cuts (iOS 16,087 → 917 kbps; Android later reached 513 kbps), and Android receiving about 25–30 fps while rendering zero after a decoder reset. Numeric telemetry did not establish which network segment caused the initial stalls.

The 1.3.92 follow-up addresses reproducible code defects:

- An unprimed decoder now requests a replacement keyframe again while deltas arrive, at most once a second. A suppressed request or lost replacement picture no longer leaves it waiting only for the ten-second periodic keyframe. Requests stop after a keyframe or tile removal; there is no additional polling timer.
- First-congestion protection records its timestamp before marking congestion observed. Its short observation window cannot collapse the estimate in one unconfirmed cut. A measured relay bottleneck, heavy uplink loss, or exact startup downlink limit still constrains the rate promptly.
- Receiver throughput is treated as downstream capacity only when that receiver reports backlog, rather than when an unrelated local/uplink queue is high.
- The panel and opt-in live samples expose `WaitingForKeyframe` and cumulative `RecoveryRequests` (browser notifications, not a count of requests delivered over the network).

Validation includes failing-before/passing-after regressions, network simulations retaining slow-link/step-down behavior, and real Chrome H.264/VP9 encode/decode with the first recovery request deliberately lost. Both codecs resumed rendering in about 1.08 seconds. This is a desktop fixture, not proof that all real-device stalls are resolved; another live phone call is required.

Funnel carries the public HTTPS/WebSocket path, including receiver-originated keyframe requests. The observed UDP media path uses negotiated TURN and does not itself traverse Funnel's TCP proxy. Funnel/control-path delay could affect recovery, but the trace does not isolate it from browser, Wi-Fi, host, ISP or TURN behavior.

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

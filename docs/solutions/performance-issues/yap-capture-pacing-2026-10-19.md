---
title: "Yap 1.3.88: Safari preparation and stale sender queues"
date: 2026-10-19
category: performance-issues
module: Bolt.Media
---

# Evidence

The opt-in 1.3.87 phone call showed Safari using WorkerWebgl. Its median preparation cost was
33.74 ms at 1080p, 14.06 ms after selecting 720p, and 31.65 ms after returning to 1080p.
Encoded rates were 16.9, 29.55 and 19.61 fps respectively. This identifies resolution-sensitive
preparation work; it does not measure CPU time, hardware codec use or device temperature.
The same call showed sender queue delays above four seconds and a UDP ICE failure followed by
WebSocket fallback and reconnection. No sample entered automatic video suspension.

# Changes

- Safari's worker probes WebGPU once when camera orientation is hidden. External camera textures
  combine YUV conversion and orientation correction in the render pass, avoiding the WebGL
  `texImage2D` upload. The final canvas-to-VideoFrame snapshot remains. Unsupported adapters,
  validation failures, snapshot failures and device loss use the existing WebGL normalization.
  Frame ownership remains bounded to one outstanding transfer; all frames and devices are released.
- Camera track dimensions preserve the displayed aspect ratio when Safari's frame dimensions
  still describe the hidden sensor orientation. Capture diagnostics split drawing and snapshot
  costs and identify WorkerWebgpu separately, including in opt-in server diagnostics.
- The pacer expires unfinished and queued video after its existing 500 ms video deadline, even
  when capture has stopped or the transport is blocked. Dependent pictures and repairs are
  discarded, and a coalesced keyframe request recovers the stream. This bounds the application
  queue, not bytes already accepted by the browser or network.
- The token bucket accommodates at least one fragment. Previously a WebSocket fragment larger
  than its maximum burst could never be sent after a low-rate cut.
- Switching paths discards old video, repairs and audio and clears pacer observations before the
  rate controller samples the new path. A new keyframe prevents continuation against lost references.
- A persistent ICE disconnect starts a serialized ICE restart after three seconds, while media
  continues through the authenticated socket. Brief disconnects and an existing restart are left alone.

# Validation

- Browser media unit suite: 174 passed, including worker fallback, ownership and diagnostics.
- Congestion, rate and client transport tests: 88 passed; additional ICE recovery and media resume
  checks: 16 passed. Regressions cover partial-picture expiry, blocked transport expiry, large
  socket fragments at 16 kbps and clearing media on a path change.
- Yap regression suite: 321 passed.
- `scripts/yap/worker-capture-check.html` checks all eight rotation/flip combinations with real
  VideoFrames in the production worker. `?webgl` exercises the compatibility path.
- Desktop Chromium synthetic 1080x1920 preparation: WebGPU sustained 30.19/60.18 fps at requested
  30/60, with mean preparation 0.53/0.43 ms; WebGL sustained 30.16/60.18 fps with 0.31/0.33 ms.
  Both paths preserved pixels. These are desktop pipeline checks, **not an iPhone speedup**.

After deployment, repeat the phone comparison with diagnostic sharing enabled. Check the actual
capture path, draw/snapshot costs, outgoing and incoming frame rates, queue delay, bitrate cuts,
UDP recovery and perceived warming. Stable 1080p60 and lower phone temperatures are not yet verified.

The external-texture mechanism is described in the [WebGPU explainer](https://gpuweb.github.io/gpuweb/explainer/#gpuexternaltexture).
WebKit also documents historical [OffscreenCanvas frame-construction costs](https://bugs.webkit.org/show_bug.cgi?id=300579);
that report alone does not establish which implementation a particular iPhone runs.

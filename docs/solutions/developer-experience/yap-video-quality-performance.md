---
title: "Yap video orientation, quality and decoder latency"
date: 2026-09-21
category: developer-experience
module: Yap / Bolt.Media
problem_type: performance
component: video calls
severity: high
tags: [yap, bolt, video, webcodecs, performance]
status: current
---

# Yap video orientation, quality and decoder latency

Prepared as Yap 1.3.59 against develop `8c071536`. This continues the [video/update review](yap-video-update-recovery.md).

## Orientation

The Android capture path cloned a VideoFrame with `rotation: 0` and then manually rotated it. WebCodecs composes the clone's rotation with inherited orientation; zero does not erase it. Drawing that frame applies orientation again. A real Chromium test reproduced wrong output pixels with the old implementation and correct upright pixels with the change.

Draw the original frame once into a reusable canvas when it has orientation metadata. Upright processor frames retain their direct path. The Safari video-element path now paints displayed pixels into an encoder-sized canvas before constructing a frame, so sensor metadata cannot leak into the elementary stream. There is no CPU pixel readback. Both endpoints should update: the sender must fix the pixels before encoding. A receiver cannot reliably infer missing rotation from an already encoded picture.

See [WebCodecs](https://www.w3.org/TR/webcodecs/) for inherited VideoFrame orientation and drawing behavior. Physical iOS-to-Android verification remains necessary; the screenshot alone does not identify which device produced the incorrect raster.

## Quality controls

Settings > Calls and the in-call Settings panel expose resolution and frame rate, saved per browser. The default target is 1080p30; choices range from 360p to 2160p (4K), with 30/60 fps ceilings. Lower adaptive tiers retain their existing 15?25 fps limits. Camera, encoder, peer decoder, battery, participant count and congestion can lower the actual result. Small camera images are not upscaled to the selected maximum. CPU core count alone no longer caps a phone at 720p, because it does not measure the dedicated video encoder.

Codec profiles/levels account for picture size and frame rate. H.264 receivers read the SPS profile/level in each keyframe rather than assuming every sender uses the same profile. Peer decoder resolution ceilings travel inside the existing encrypted control envelope; older clients default to a conservative 1080p ceiling. Unsupported encoder configurations first fall from 60 to 30 fps, then down the resolution ladder. Sustained pressure also reduces 60 fps to 30 before reducing resolution.

4K60 is a requested ceiling, not a device capability promise. The development machine rejected hardware-preferred 4K60. Camera/decoder limitations can still require lower quality on a physical phone. The original bounded fragment format, authenticated encryption, epoch checks and packet-size limits are unchanged; oversized keyframes are dropped and contribute to adaptation rather than weakening those bounds.

## Performance changes

- Enforce the chosen frame rate before conversion/encoding, including cameras that keep producing 60 fps when the target is 30.
- Apply lower capture constraints as adaptation changes, reducing camera work where the browser supports it.
- Queue complete encoded pictures before allocating fragment arrays; pictures rejected by the bounded queue allocate no fragments.
- Connect local video streams to receiver bitrate/keyframe feedback. Previously only remote streams had controllers, so feedback for the locally published video stream was ignored.
- Give every fragment of a picture the same 90 kHz capture timestamp. Jitter is sampled once per picture; packet loss is still tracked per sequence. A large fragmented picture no longer looks like many late 33 ms pictures.
- Permit high-resolution bitrate targets above the previous 10 Mbps controller ceiling.
- Stop sending legacy plaintext bandwidth probes on authenticated streams; the encrypted relay rejects them. Actual receiver feedback and encoder/queue measurements drive adaptation.
- Measure decoder submission-to-output delay using the local clock. After twelve consecutive H.264 pictures exceed 100 ms at up to 1080p, try a supported software decoder once and request a fresh keyframe. Restore the browser's default if software also falls behind, errors, or the image grows beyond 1080p. Never continually alternate decoder choices. Timestamp tracking is capped at 32 entries per stream. 1440p/4K keep the browser's native decoder choice.

## Native-browser measurements

Windows Chromium via agent-browser, localhost, native WebCodecs H.264, synthetic canvas source with changing colors. Forced the video-element capture strategy used on Safari, encoded portrait pixels, and passed encoded chunks directly into the native decoder. This isolates capture/codec/render work; it excludes .NET interop, SFrame, relay, network, real camera capture and complex moving scenes. It does not establish mobile CPU or battery cost.

| Local loop | Median capture-to-render | 95th percentile | Notes |
|---|---:|---:|---|
| 360x640, default decoder | 289.1 ms | 323.0 ms | Encoding median 5.0 ms; decoding median 283.3 ms |
| 360x640, automatic fallback, settled | 7.0 ms | 17.2 ms | One decoder transition/keyframe request; 154 encoded / 146 rendered across six seconds |
| 1080x1920, default decoder | 157.8 ms | 182.2 ms | Last three seconds of six-second run; no fallback with the exploratory 150 ms threshold |
| 1080x1920, automatic fallback, settled | 21.4 ms | 30.0 ms | Last three seconds; final 100 ms threshold; one transition; 137 encoded / 133 rendered overall |

The automatic fallback's full-run 1080p p95 was 174.1 ms: initial buffered frames are deliberately measured before switching. The settled result must not be described as immediate, end-to-end latency. Submitted frame rates varied with browser scheduling; these runs do not prove sustained 30 or 60 fps on real cameras. The measured delay reduction justifies a conditional fallback, not universally forcing software decoding.

Raw measurements and scripts from this run are in the local ignored `artifacts/yap/video-quality-browser*` files. For reproduction, serve the module on localhost, feed a canvas captureStream into getUserMedia, select `useCaptureStrategy('rvfc')`, and timestamp `_encodeElementFrame`, `OnVideoEncoded` and `_render` using performance.now(). Warm up for three seconds; report the full-run and settled distributions separately. Compare the same resolution, codec, browser and source content.

## Server and AOT review

Release builds already set `RunAOTCompilation=true`. WebCodecs invokes native browser encoders/decoders; JavaScript orchestrates them. Moving raw frames through C# AOT would add transfers and would not turn a managed codec into the browser's hardware codec. Keep raw pixels out of .NET, and optimize encoded-data crossings where profiling warrants it. See [Blazor AOT](https://learn.microsoft.com/en-us/aspnet/core/blazor/webassembly-build-tools-and-aot?view=aspnetcore-10.0).

`BoltServer.RouteMediaFrameCoreAsync` forwards opaque authenticated packets without decoding/transcoding. `BoltHubConnection` already uses pooled byte buffers, bounded queues, recipient backpressure and batched transport writes. No server implementation change was justified by the local codec measurement. The corrected sender feedback and removed rejected probes improve use of this existing relay.

Remaining substantial costs: reliable WSS/TCP head-of-line blocking, serial per-fragment SFrame/JS interop, and the call-wide routing gate around authorization/roster consistency. Do not remove that gate or authentication checks as a speculative optimization. A next experiment should measure sender encode, SFrame, relay enqueue/send and receiver decode separately on two physical phones, then evaluate batched encrypted operations while preserving epoch atomicity and bounds. A WebRTC media transport would be a separate architecture decision, not an AOT switch.

The prior review's mid-call codec-family-change/published-stream mismatch remains separate; quality changes within the existing codec do not introduce a new stream format.

## Validation

- Blazor client build passed with three existing SQLite WASM warnings.
- Yap client tests: 244 passed.
- Bolt media/video/SFrame tests: 89 passed, including sender feedback wiring and fragment-burst jitter regressions.
- Browser JavaScript suites: 348 passed (Yap UI, encryption and Bolt media).
- Real Chromium: orientation pixel comparison, 1080p portrait encode/decode, native/software buffering comparison and automatic fallback.
- Still required: physical iPhone/Android orientation, camera switching, 1080p/4K support, sustained calls and thermal behavior on mobile networks.


## 1.3.60: quality collapse, fragment loss, and Safari capture

Follow-up to the Android/iOS device reports on 21 September 2026. The reports do not contain stage timings, so these fixes address reproduced code defects and a Safari capture risk; they do not prove the exact cause of every frozen phone call.

- **Fragment loss reproduced:** the client dispatched every incoming fragment as an independent asynchronous decrypt operation. The shared SFrame bridge admits 32 pending operations, while a legal video picture has up to 96 fragments. Holding the first JS operation during a 96-packet burst delivered only 32 packets through the old pattern; the new bounded, ordered ingress delivered all 96 in sequence. Real decryption/authentication failures still fail closed. Per-stream ciphertext and video playback queues remain bounded to 192 packets (two maximum-sized pictures each), and over-capacity live traffic still drops oldest packets.
- **Quality collapse:** receiver feedback used lifetime packet loss, repeatedly penalizing one old loss event. Feedback now uses packets/loss since the previous report; idle reports maintain rather than repeat stale advice. Camera cadence alone no longer lowers resolution: low-light exposure or browser scheduling can lower cadence with no overload. An actually full encoder queue, send drops or current receiver feedback still lower quality. Waiting for a replacement keyframe no longer counts each discarded dependent picture as another congestion event or repeatedly requests a keyframe.
- **Safari capture:** local preview and encoding previously used two separate video elements; the encoding element was invisible. Encoding now follows the visible preview when mounted, cancels callbacks on the old element, and preserves ownership of the UI's video node. This removes reliance on the invisible duplicate while the user sees a live preview. Safari visibility policies make that a plausible stall path, but the physical iPhone-to-Android symptom still needs retesting. See [WebKit video policies](https://webkit.org/blog/6784/new-video-policies-for-ios/).
- **First-picture recovery:** start the bounded send pump before camera capture can emit a keyframe. Mounting a remote canvas requests a fresh keyframe so startup/expansion does not wait for the periodic interval.
- **Accepting video:** video intent is carried in the initial invitation, independently of current camera roster state. The primary button says **Accept video**, with **Audio only** as an explicit alternative. No camera opens just because an invitation arrives; camera setup follows acceptance and encrypted epoch readiness. Legacy invitations can still indicate video through camera roster state.
- **Interop:** synchronous SFrame Rust operations use in-process JS interop where the WASM runtime supports it, keeping the existing lock, pending bound, sender binding and epoch checks. Other runtimes retain async interop. Raw video pixels continue through native WebCodecs rather than .NET.

### Follow-up measurements

The [reproducible benchmark](../../../scripts/yap/video-benchmark/README.md) adds actual Blazor WASM fragmentation, SFrame encrypt/decrypt and a local WebSocket echo to the codec pipeline. Windows Chromium, synthetic moving 1920x1080 camera, 1080p30 requested, 12-second runs, non-AOT measurement host. Both runs triggered the existing conditional software decoder fallback.

| Measurement | In-process SFrame | Async-only comparison |
|---|---:|---:|
| Settled capture-to-render median / p95 | 15.9 / 27.6 ms | 19.0 / 26.3 ms |
| Full-run fragment + crypto + interop + echo median / p95 | 3.1 / 5.4 ms | 3.4 / 6.1 ms |
| Encoded / rendered pictures | 271 / 267 | 272 / 268 |
| Sender queue drops / errors | 0 / 0 | 0 / 0 |

The small interop difference does not establish a dramatic speedup; run-to-run scheduling noise remains. Camera scheduling yielded about 22?23 encoded fps, so this does not prove sustained 30fps. Settled timing excludes the first three seconds; tails and decoder resets explain the difference between encoded and rendered counts. These are loopback measurements, not Funnel or phone-to-phone latency. There is no claim that the actual Bolt relay, simultaneous audio, cellular networks, or physical iOS were benchmarked.

The relay review found opaque forwarding with bounded queues and recipient pressure handling; group authorization is renewed at five-second intervals. There is no server codec/transcoding workload for AOT to remove. Actual remote relay timing and device thermal/codec behavior remain unmeasured. Preserve authorization/roster consistency while measuring those costs; this change does not weaken the routing gate.


Validation for this follow-up: 103 Bolt media/video/SFrame/group tests, 245 Yap client tests, 53 gateway tests, and 86 browser media tests passed. The client and standalone benchmark build successfully. Remaining acceptance work is a sustained two-phone call (iOS sending to Android, camera off/on, audio-only answer, minimizing/expanding, and genuine bandwidth reduction).

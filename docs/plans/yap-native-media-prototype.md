# Yap native media prototype

## Objective

Measure whether moving capture, H.264 encode/decode and display out of the browser improves sustained two-device 1080p calls and thermal behaviour on iPhone and Galaxy S25 Ultra. Preserve the existing Yap web UI. Do not introduce downloadable DLLs, replace production calls, or claim that native code alone guarantees lower temperatures.

## First experiment

Two separate .NET 10 platform hosts (Android and iOS) load the existing HTTPS Yap UI. A dedicated `/diagnostics/native-media` page controls a native benchmark via a small, origin-restricted bridge. Native video occupies native views, not an HTML canvas. Only commands and one-second numeric samples cross the bridge; raw and compressed frames never do.

* Android: Camera2 recording session, MediaCodec input Surface, hardware H.264 encoder/decoder, native output Surface.
* iOS: AVCaptureSession camera buffers directly into VideoToolbox H.264 compression, native AVSampleBufferDisplayLayer playback. No WebGL/WebGPU upload or CPU pixel conversion.
* First test: video-only, both directions, 1920×1080 landscape sensor buffers, 30 FPS then 60 FPS when the selected camera supports it. Fail explicitly if the requested capture/codec mode is unsupported; do not silently downgrade. Receiver orientation is metadata.
* Controlled same-LAN connection: bounded encrypted TLS stream, ephemeral self-signed certificate pinned by an out-of-band invitation plus a random pairing secret. This is an isolated benchmark transport, **not Bolt wire compatibility, STUN/TURN or production signalling**. TCP head-of-line blocking is an explicit comparison limitation. A stalled peer ends the run instead of creating an unbounded queue.
* No microphone recording in this first video experiment. Compare with web calls muted on both ends. Audio, authenticated Yap call admission, SFrame, native ICE and Bolt transport integration are later work, gated on evidence that native video helps.
* Existing PWA remains usable and unchanged for normal calls. The prototype has a distinct application identifier and explicit test UI.

## Implementation sequence

1. Shared benchmark configuration, pairing, bounded framing, lifecycle, numeric sampling and tests.
2. Native Android capture/encode/display with permission, stop/background and codec-error handling.
3. Native iOS capture/encode/display with permission, stop/background and thermal-state sampling.
4. Existing web UI diagnostic page and origin-restricted native control bridge.
5. Build Android APK; build/sign iOS on the user's Mac. Verify front/rear and orientation on physical phones.
6. Run paired 1080p30 tests, then 1080p60 only where supported. Export measurements and compare with current web diagnostics.

## Acceptance and benchmark protocol

Use the same two phones, Wi-Fi, codec, target bitrate (initially 6 Mbps per direction), brightness, camera, scene and test duration. Remove chargers; start each run at a comparable thermal state and battery level. Let devices cool between runs; alternate test order. Use Release builds without attached debuggers. Record OS/browser/app versions and whether Tailscale is enabled. Prefer a plain LAN run for this initial native test, and report that route difference from the web baseline.

Run 2 minutes of warm-up plus 10 minutes of measurement at 1080p30. Repeat at 60 FPS only if capture supports the selected size/rate combination. Measure actual capture, encode, receive and decoder-output/display-submission rates separately; report output callbacks as such, never as measured physical screen presentation. Include queue drops, inter-frame gaps, process CPU, memory, battery, Android thermal status/battery temperature, and iOS ProcessInfo thermal state (not degrees Celsius). Capture Instruments Energy/Time Profiler and Android Perfetto traces for attribution when available. Do not infer GPU use from a hardware preference.

Pass requires uninterrupted two-way picture, correct front/rear orientation, bounded memory and queues, clean stop/background cleanup, and measured sustained improvement. Numeric unit tests and desktop builds cannot establish phone temperatures or real 60 FPS. Publish raw JSONL plus comparison summaries; do not declare a thermal improvement before paired physical-device runs.

## Build prerequisites and current evidence

Android Release APK and iOS Release device builds pass. The Mac uses Xcode 26.2 and iOS workload 26.2.10191. Ten shared transport/lifecycle tests pass. Both native apps are installed on the supplied phones; the iPhone build uses the newly added Personal Team rather than either company team. Yap 1.3.95 publishes the benchmark page, verified on Android. Physical two-phone capture and heat measurements remain pending; see the [testing procedure](yap-native-media-testing.md). This plan deliberately does not promise store distribution or background calling.

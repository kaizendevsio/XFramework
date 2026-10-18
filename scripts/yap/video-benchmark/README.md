# Encrypted video browser benchmark

Run from the repository root, with .NET 10, Python and Chromium available:

```powershell
dotnet publish scripts/yap/video-benchmark/Bench.csproj -c Release -o artifacts/yap/video-stalls-publish
python -m pip install --target artifacts/yap/bench-deps websockets==17.1
python scripts/yap/video-benchmark/server.py
```

Open http://127.0.0.1:8788 in Chromium. In the browser console run `await runBench()`.
Pass `await runBench(false, 1440, 60)` to measure the new default request. The result includes actual encoded/rendered fps and capture counts.

Reload, then run `await runBench(true)` to compare async-only SFrame interop. Close the page and stop the server when finished.

This uses a synthetic 1080p canvas camera, the production Safari-style capture/encoder/decoder, production Blazor WASM fragment assembly and SFrame Rust WASM encryption, and a loopback WebSocket echo. It does not use real credentials, contact another user, or require camera permission. The payload stays encrypted during the WebSocket round trip. The async-only comparison wraps JS references to exercise the former async bridge; all other code stays identical.

Runs last 12 seconds. Capture-to-render percentiles exclude the first three seconds (decoder warm-up/fallback); crypto/interop/echo distributions cover the full run. Encoded/rendered totals include startup and shutdown; unrendered tail frames and decoder reset losses are not counted as sender queue drops. This benchmark does **not** establish 30fps, phone performance, WAN delay, audio coexistence, or Bolt relay throughput: the echo replaces the relay and its packet routing. The receive-burst tests in BoltSFrameInteropTests cover the production ingress queue separately.

The host is intentionally a small non-AOT measurement harness; it uses the same production media library. Compare distributions, frame counts and drops, not a single timing sample. Do not use these loopback measurements as claimed phone-to-phone latency.

## Capture-only comparison

`scripts/yap/video-capture-benchmark.html` isolates camera ingestion and encoding from transport/crypto.
From the repository root, save the chosen baseline `bolt-media.js` into
`artifacts/bolt-media-capture-baseline.js`, then serve the repository on loopback:

```powershell
git show 9fa1eefd:src/Libraries/Bolt/Bolt.Media.Browser/wwwroot/bolt-media.js | Set-Content artifacts/bolt-media-capture-baseline.js
python -m http.server 8791 --bind 127.0.0.1
```

Open `http://127.0.0.1:8791/scripts/yap/video-capture-benchmark.html` and run `void runAll()`.
Results accumulate in `window.results`; `window.finished` and `window.failure` report completion.
The harness alternates three repeats of baseline WebGL, current WebGL and direct processor capture
at 1080p30 and 1440p30, with two seconds of warmup and six measured seconds per run.
It uses a synthetic canvas camera and real WebCodecs encoder. A temporary vendor override selects
the production WebKit WebGL branch on Chromium; it does not emulate Apple's GPU/media stack.
Direct processor capture measures the shared frame-ingestion method, **not** Safari worker transfer
overhead. These measurements establish neither thermal savings nor end-to-end delivery performance.
Do not run builds or other benchmarks concurrently when collecting comparison evidence.

### 19 October 2026 capture measurements

The [raw 18-run results](../../../docs/solutions/performance-issues/yap-video-capture-2026-10-19.json)
compare baseline `9fa1eefd` with the worker-capture change on Windows Chromium / NVIDIA RTX 3070.
Local compilation was idle during this final set. Median of three per-run p95 callback times:

| Capture path | 1080p30 | 1440p30 |
| --- | ---: | ---: |
| Baseline WebGL | 2.9 ms | 4.4 ms |
| Current WebGL | 1.7 ms | 2.6 ms |
| Direct processor ingestion | 0.1 ms | 0.1 ms |

WebGL's measured p95 cost fell about 41%; encoded FPS stayed approximately 28.5–29.2.
Processor runs were approximately 27.1–29.0 FPS. This is not evidence of higher delivered FPS,
Safari worker throughput, iPhone hardware acceleration, or reduced phone temperature. Hardware
preference is reported separately from the renderer; it is not proof of hardware encoding.
The preliminary run overlapped compilation and is excluded. Device validation should compare
sustained calls at the same quality and inspect `worker-processor` versus `rvfc/webgl` diagnostics.

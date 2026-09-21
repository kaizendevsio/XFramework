# Encrypted video browser benchmark

Run from the repository root, with .NET 10, Python and Chromium available:

```powershell
dotnet publish scripts/yap/video-benchmark/Bench.csproj -c Release -o artifacts/yap/video-stalls-publish
python -m pip install --target artifacts/yap/bench-deps websockets==17.1
python scripts/yap/video-benchmark/server.py
```

Open http://127.0.0.1:8788 in Chromium. In the browser console run `await runBench()`.
Reload, then run `await runBench(true)` to compare async-only SFrame interop. Close the page and stop the server when finished.

This uses a synthetic 1080p canvas camera, the production Safari-style capture/encoder/decoder, production Blazor WASM fragment assembly and SFrame Rust WASM encryption, and a loopback WebSocket echo. It does not use real credentials, contact another user, or require camera permission. The payload stays encrypted during the WebSocket round trip. The async-only comparison wraps JS references to exercise the former async bridge; all other code stays identical.

Runs last 12 seconds. Capture-to-render percentiles exclude the first three seconds (decoder warm-up/fallback); crypto/interop/echo distributions cover the full run. Encoded/rendered totals include startup and shutdown; unrendered tail frames and decoder reset losses are not counted as sender queue drops. This benchmark does **not** establish 30fps, phone performance, WAN delay, audio coexistence, or Bolt relay throughput: the echo replaces the relay and its packet routing. The receive-burst tests in BoltSFrameInteropTests cover the production ingress queue separately.

The host is intentionally a small non-AOT measurement harness; it uses the same production media library. Compare distributions, frame counts and drops, not a single timing sample. Do not use these loopback measurements as claimed phone-to-phone latency.

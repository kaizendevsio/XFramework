# Bolt Performance Optimization Results

Date: 2026-07-25 (Asia/Singapore)

## Outcome

The recommended client, Hub, large-RPC, and benchmark-integrity work from
`bolt-performance-optimization-investigation-2026-07-24.md` is implemented and tested.

The strongest result is the routed payload path. On both Windows and Linux, Bolt beat the
matched routed gRPC path at every measured size from 512 KiB through 20 MiB, with
non-overlapping BenchmarkDotNet intervals and about one quarter of gRPC's managed allocation.

This is not a blanket all-scenario certification. Two limitations remain explicit:

- `BoltServer` local handlers do not consume the client large-RPC stream protocol. Direct Bolt
  therefore works for unary payloads but cannot run the 2-20 MiB streamed cases. The benchmark
  now fails this path clearly instead of hanging or being mislabeled as routed traffic.
- Windows peak working set is not uniformly lower for Bolt at large payloads even though
  per-operation managed allocation is substantially lower. Pool retention and process peak
  memory measure different things.

## Retained Changes

### Hub receive and send paths

- Fragment assembly buffers are returned immediately after awaited dispatch and exactly once
  on success, malformed input, cancellation, handler failure, and disconnect.
- A socket close during fragmented receive exits without dispatching incomplete bytes.
- `BoltServerOptions.ReceiveBufferBytes` is configurable and clamped to the frame limit.
- Pending-send capacity signaling allocates a waiter only when a producer actually blocks.
- Physical-send timeout state is reused per connection. A timed-out write retires that state;
  cancellation-ignoring transports keep ownership until the physical write completes.

### Client RPC path

- Invocation selects a connection once and uses one deadline source for caller cancellation and
  the RPC deadline.
- Pooled RPC responses are consumed directly as `ValueTask` values without `AsTask` allocation.
- Static `LoggerMessage` delegates and timestamp values replace per-call logging allocation.
- Asynchronous RPC continuations are enabled by default after winning the 500 and 1,024
  concurrency A/B tests. They keep user continuations off the receive loop.
- Oversized requests return `413 Request Entity Too Large` before connection selection.
- Reliable-send completions and RPC calls retain the original 256-object pool capacity. The
  tested 1,024 capacity was not retained because its 1,024-concurrency routed result and memory
  profile regressed.

### Large RPC

- The default logical request/response limit is 32 MiB and the aggregate client reassembly
  budget is 64 MiB.
- The built-in large-request and large-response collectors are installed before stream payload
  chunks can enter the generic stream channel.
- The large-RPC pipeline remains bounded by a 2 MiB byte window and flushes physical sends before
  `StreamClose`.
- The default stream chunk is 131,051 bytes, keeping a complete `StreamData` frame within
  128 KiB. It is the best measured latency/memory balance, although it uses more CPU than the
  256 KiB value at 20 MiB.

### Deliberately not retained

- A 1,024 completion-pool cap was measured and rejected.
- A smaller Hub receive-buffer default was measured and rejected for the performance default.
  The option remains available for memory-constrained deployments.
- Failure-only unary send completion was not introduced. Current completion ownership is
  frame-scoped; removing positive completion consumption would prevent safe pool return without
  adding a second request-ID lifecycle. Transport-failure behavior remains prompt and tested.
- No owned-response public API was added. Bolt already wins managed allocation, while exposing
  pooled response ownership would add a high-risk disposal contract.
- No manual SIMD, unsafe parser, hash, or dictionary rewrite was added. Windows traces showed no
  codec hotspot, and .NET 10 already uses AVX2 in the relevant span operations.

## A/B Decisions

### RPC continuations

Three randomized repetitions were run at each concurrency. Values are averages from the
request-level harness.

| Calls | Path | Inline mean / p95 | Async mean / p95 | Decision |
|---:|---|---:|---:|---|
| 500 | Direct Bolt | 3.931 / 8.233 ms | 1.254 / 1.398 ms | Async |
| 500 | Routed Bolt | 3.704 / 6.031 ms | 2.169 / 5.162 ms | Async |
| 1,024 | Direct Bolt | 9.435 / 22.016 ms | 4.055 / 15.051 ms | Async |
| 1,024 | Routed Bolt | 10.262 / 17.045 ms | 7.492 / 20.572 ms | Async; mean, p99, and throughput win |

At 1,024 routed calls, async p95 was mixed, but it improved mean in all three repetitions,
increased throughput by about 44%, improved average p99, and added only about 0.3 MiB of managed
peak.

### Completion pools

| Calls | Path | 256 mean / p95 | 1,024 mean / p95 | Decision |
|---:|---|---:|---:|---|
| 500 | Direct Bolt | 2.505 / 13.160 ms | 2.398 / 12.072 ms | Mixed |
| 500 | Routed Bolt | 4.101 / 21.843 ms | 2.129 / 3.869 ms | 1,024 wins |
| 1,024 | Direct Bolt | 3.758 / 8.095 ms | 3.463 / 10.500 ms | Mixed |
| 1,024 | Routed Bolt | 7.508 / 17.264 ms | 9.236 / 20.036 ms | 256 wins |

The 1,024 pool also retained about 1.5 MiB more managed peak in the 1,024 routed case. The final
capacity is 256.

### Hub receive buffers at 1,000 active clients

| Receive buffer | Mean / p95 | Throughput | CPU | Baseline managed heap |
|---:|---:|---:|---:|---:|
| 64 KiB | 11.217 / 14.432 ms | 76,570 req/s | 1,938 ms | 176.6 MiB |
| 128 KiB | 12.091 / 16.041 ms | 76,844 req/s | 1,984 ms | 238.7 MiB |
| 256 KiB | 8.852 / 12.059 ms | 108,187 req/s | 1,401 ms | 363.6 MiB |

The 256 KiB default is retained for maximum performance. A 64 KiB deployment setting saves about
187 MiB of managed baseline per 1,000 connections but gives up substantial peak throughput.

### Final 20 MiB chunk sweep

| Chunk payload | Mean / p95 / p99 | CPU | Working-set delta | Heap delta |
|---:|---:|---:|---:|---:|
| 65,515 | 39.428 / 61.257 / 93.169 ms | 8,484 ms | 343.0 MiB | 289.5 MiB |
| 131,051 | 39.353 / 67.436 / 78.482 ms | 8,849 ms | 249.0 MiB | 248.5 MiB |
| 262,123 | 47.522 / 76.428 / 112.003 ms | 7,693 ms | 241.7 MiB | 273.3 MiB |
| 262,144 | 43.250 / 72.229 / 103.007 ms | 7,625 ms | 243.1 MiB | 255.2 MiB |

The 128 KiB-aligned value has the best mean and p99 while avoiding the 64 KiB working-set spike.
Its CPU cost versus 256 KiB is documented and the value remains configurable.

## Bolt Versus gRPC

### Windows routed BenchmarkDotNet

Job: .NET 10, server GC, AVX2, 3 launches, 5 warmups, 15 measurements, 250 ms minimum
iteration time. Windows 11 ran under Hyper-V.

| Payload | Bolt mean | gRPC mean | Bolt allocation | gRPC allocation |
|---:|---:|---:|---:|---:|
| 512 KiB | 2.282 ms | 4.326 ms | 517.94 KiB | 2,090.24 KiB |
| 1 MiB | 4.423 ms | 9.677 ms | 1,030.68 KiB | 4,162.02 KiB |
| 2 MiB | 5.516 ms | 18.438 ms | 2,096.55 KiB | 8,397.49 KiB |
| 5 MiB | 10.658 ms | 37.896 ms | 5,217.30 KiB | 21,113.92 KiB |
| 10 MiB | 18.626 ms | 71.475 ms | 10,419.55 KiB | 42,271.69 KiB |
| 20 MiB | 32.843 ms | 133.674 ms | 20,813.03 KiB | 84,567.28 KiB |

The 99.9% intervals do not overlap for any routed row.

### Linux routed BenchmarkDotNet

Environment: `xeon-dev`, Linux x64, 6 logical CPUs, 16 GiB RAM, official .NET 10 SDK
container, server GC, AVX2. The same credible job and payload matrix were used.

| Payload | Bolt mean | gRPC mean | Bolt allocation | gRPC allocation |
|---:|---:|---:|---:|---:|
| 512 KiB | 3.725 ms | 7.535 ms | 524.29 KiB | 2,099.97 KiB |
| 1 MiB | 7.268 ms | 12.928 ms | 1,043.04 KiB | 4,181.40 KiB |
| 2 MiB | 9.117 ms | 23.311 ms | 2,124.14 KiB | 8,449.43 KiB |
| 5 MiB | 20.142 ms | 58.166 ms | 5,282.17 KiB | 21,232.76 KiB |
| 10 MiB | 40.115 ms | 110.914 ms | 10,545.65 KiB | 42,501.19 KiB |
| 20 MiB | 78.897 ms | 209.886 ms | 21,090.96 KiB | 85,154.38 KiB |

Linux independently confirms the Windows direction and allocation ratio.

### Request-level routed tails on Windows

| Payload | Bolt mean / p95 / p99 | gRPC mean / p95 / p99 |
|---:|---:|---:|
| 512 KiB | 2.658 / 6.979 / 19.327 ms | 4.350 / 16.307 / 23.717 ms |
| 1 MiB | 4.891 / 10.953 / 23.093 ms | 8.965 / 25.318 / 32.133 ms |
| 2 MiB | 6.091 / 21.298 / 27.299 ms | 17.090 / 36.415 / 46.440 ms |
| 5 MiB | 12.959 / 31.191 / 50.602 ms | 35.650 / 66.978 / 80.400 ms |
| 10 MiB | 20.850 / 40.559 / 65.927 ms | 69.460 / 103.684 / 147.059 ms |
| 20 MiB | 37.847 / 59.490 / 75.408 ms | 132.707 / 186.711 / 226.227 ms |

These are closed-loop request timings and can understate queueing tails under a fixed arrival rate.

## Verification

- Release build: passed. Existing warnings remain outside this change.
- Focused performance/lifecycle tests: 80 passed before final integration; the final changed
  receive and client lifecycle set passed 25/25.
- Complete locally runnable .NET suite: 513 passed, 7 existing durable Redis tests skipped.
- PostgreSQL Bolt authorization fixture: 4 passed through a loopback-only SSH/Tailscale tunnel
  to `xeon-dev` Docker 29.3.0. The tunnel was stopped and no Testcontainers remain.
- Browser build and protocol/lifecycle suite: 18 passed.
- Linux Release build: passed in the official .NET 10 SDK container.
- `git diff --check`: passed; only line-ending conversion notices were reported.

## Raw Artifacts

- [Windows routed BDN](bolt-performance-results-2026-07-25/payload-bdn-routed-final/results/Bolt.Tests.RoutedPayloadBenchmarks-report-github.md)
- [Windows direct BDN](bolt-performance-results-2026-07-25/payload-bdn-direct-final/results/Bolt.Tests.DirectPayloadBenchmarks-report-github.md)
- [Linux routed BDN](bolt-performance-results-2026-07-25/linux-final/routed/results/Bolt.Tests.RoutedPayloadBenchmarks-report-github.md)
- [Request-level payload matrix](bolt-performance-results-2026-07-25/payload-latency-final)
- [Continuation A/B at 500](bolt-performance-results-2026-07-25/continuation-ab-final)
- [Continuation A/B at 1,024](bolt-performance-results-2026-07-25/continuation-ab-1024-final)
- [Pool-capacity A/B](bolt-performance-results-2026-07-25/pool-cap-ab-final)
- [Hub 1,000-client matrix](bolt-performance-results-2026-07-25/hub-buffer-scale-1000-active-final/hub-connection-scale-results.json)
- [Final chunk sweep](bolt-performance-results-2026-07-25/chunk-sweep-20m-final/request-latency-results.json)

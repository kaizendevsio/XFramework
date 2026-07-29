# Bolt WebSocket Performance Certification

Date: 2026-07-22 to 2026-07-23 (Asia/Singapore)

## Verdict

**Not certified against the strict gate.**

Wire v2, zero-copy send ownership, pooled reliable completions, batching, bounded large-RPC buffering, 256 KiB stream chunks, and a 2 MiB pipeline byte window are implemented. Bolt now wins the measured direct, Hub, sustained-throughput, and most concurrency cases on mean, p95, and allocation. It is not certified in every case:

- The complete final paired payload matrix did not finish because the isolated gRPC 2 MiB worker stopped making progress.
- Historical 2 MiB p95 and 5 MiB confidence-interval failures have not both been replaced by a complete final paired run.
- 20 MiB now completes after the Hub backpressure fix, but its p95 is slightly worse and the cross-run confidence intervals overlap.
- Concurrency 500 has better Bolt point estimates, but overlapping 95% mean intervals make it a statistical tie.
- The credible independent-client scalability run did not finish, and the 32 MiB maximum is covered by tests but not BenchmarkDotNet.

Per the approved plan, Linux and out-of-process Tailscale certification were not run after the local gate failed. No TCP or QUIC RPC work is proposed here.

## Build Under Test

- Branch: `codex/bolt-remediation-plan-scope`
- Base commit: `c4682cb1dfb94f4e22e983fcd1ccb6981c41c4b9`
- Worktree: uncommitted implementation under review
- Bolt wire version: `2`
- Runtime: .NET 10.0.0, SDK 10.0.100, x64 RyuJIT AVX2, concurrent server GC
- BenchmarkDotNet: 0.14.0
- Host: Windows 11 build 26200.8875 under Hyper-V
- CPU: Intel Core Ultra 7 265K, 18 cores / 18 logical processors
- Job: 3 launches, 5 warmups, 15 measured iterations, 250 ms minimum iteration time
- Topology: in-process loopback client and server; one Bolt WebSocket connection unless the scenario states otherwise

Hyper-V and the baseline's shorter job weaken before/after comparability. Current Bolt-versus-gRPC rows use the same credible job and machine.

## Results

### Direct And Hub RPC

| Path | Concurrency | Bolt mean / p95 | gRPC mean / p95 | Bolt alloc | gRPC alloc | Result |
|---|---:|---:|---:|---:|---:|---|
| Hub | 1 | 239.1 / 301.6 us | 397.1 / 557.9 us | 6.18 KiB | 19.72 KiB | Pass |
| Direct | 1 | 117.7 / 140.6 us | 214.6 / 300.6 us | 3.84 KiB | 8.64 KiB | Pass |
| Hub | 64 | 650.9 / 835.2 us | 2062.8 / 2915.0 us | 205.03 KiB | 1304.16 KiB | Pass |
| Direct | 64 | 558.0 / 1212.7 us | 1913.5 / 2639.4 us | 153.33 KiB | 583.56 KiB | Pass |

`Op/s` in this benchmark is batch operations per second at concurrency 64, not individual RPCs per second.

### Sustained Throughput

| Path | Bolt op/s | gRPC op/s | Bolt alloc/op | gRPC alloc/op | Result |
|---|---:|---:|---:|---:|---|
| Hub, 100-request batch | 58,883 | 17,881 | 3.16 KiB | 20.25 KiB | Pass |
| Direct, 100-request batch | 89,314 | 25,606 | 2.36 KiB | 9.04 KiB | Pass |
| Sequential 1 KiB | 3,817.9 | 3,127.7 | 6.59 KiB | 10.32 KiB | Pass |
| Sequential 64 KiB | 1,593.4 | 1,007.0 | 71.47 KiB | 139.19 KiB | Pass |

The batch-throughput p95 is normalized from a 100-request batch duration; it is not request-level tail latency.

### Single-Connection Concurrency

| Calls | Bolt mean / p95 | gRPC mean / p95 | Bolt alloc/batch | gRPC alloc/batch | Result |
|---:|---:|---:|---:|---:|---|
| 10 | 0.397 / 0.558 ms | 0.488 / 0.667 ms | 45.09 KiB | 108.32 KiB | Pass |
| 50 | 0.850 / 1.278 ms | 1.611 / 2.073 ms | 206.31 KiB | 556.90 KiB | Pass |
| 100 | 1.680 / 2.700 ms | 2.595 / 3.527 ms | 406.55 KiB | 1116.94 KiB | Pass |
| 500 | 5.947 / 7.930 ms | 6.380 / 8.163 ms | 2037.17 KiB | 5523.16 KiB | **Fail: 95% CI overlap** |

For 500 calls, approximate 95% mean intervals are Bolt `[5.606, 6.287] ms` and gRPC `[6.036, 6.724] ms`. The benchmark fixture raises only its own Hub pending-RPC and client inbound-handler admission limits to 1024 so the declared 500-call workload is measured; production defaults are unchanged.

### Latest Payload Results After Focused Tuning

This table replaces the pre-tuning payload snapshot with the newest valid result available for each size. The latest paired run completed all three launches for 512 KiB and 1 MiB, then stalled during the second gRPC launch at 2 MiB. The unfinished gRPC 2 MiB sample is intentionally omitted. Rows that were not rerun retain the earlier same-machine value and are labeled accordingly.

| Payload | Bolt mean / p95 | gRPC mean / p95 | Bolt alloc | gRPC alloc | Evidence and result |
|---:|---:|---:|---:|---:|---|
| 100 B | 0.196 / 0.247 ms | 0.303 / 0.491 ms | 5.84 KiB | 8.68 KiB | Earlier paired result: pass |
| 1 KiB | 0.266 / 0.375 ms | 0.357 / 0.471 ms | 6.74 KiB | 10.48 KiB | Earlier paired result: pass |
| 32 KiB | 0.377 / 0.436 ms | 0.498 / 0.686 ms | 37.76 KiB | 73.47 KiB | Earlier paired result: pass |
| 128 KiB | 1.010 / 1.294 ms | 1.263 / 1.513 ms | 135.40 KiB | 268.90 KiB | Earlier paired result: pass |
| 512 KiB | 3.706 / 4.649 ms | 3.924 / 4.880 ms | 549.96 KiB | 1141.42 KiB | Latest paired row: directional win; mean CI overlaps |
| 1 MiB | 7.689 / 9.824 ms | 8.337 / 10.299 ms | 1253.78 KiB | 2349.43 KiB | Latest paired row: directional win; mean CI overlaps |
| 2 MiB | 14.094 / 17.350 ms | Not completed | 2962.75 KiB | Not completed | Latest run incomplete; no comparison claim |
| 5 MiB | 22.449 / 26.945 ms | 25.779 / 31.588 ms | 5.11 MB | 10551.63 KiB | Latest Bolt-only tuning result versus earlier gRPC: directional win |
| 10 MiB | 35.522 / 45.322 ms | 47.227 / 57.260 ms | 10440.98 KiB | 21137.14 KiB | Earlier paired result: pass |
| 20 MiB | 88.449 / 110.708 ms | 91.557 / 108.397 ms | 20946.14 KiB | 42263.70 KiB | Post-fix Bolt-only result versus earlier gRPC: p95 and CI still fail |

The completed 512 KiB and 1 MiB values were reconstructed from the per-iteration results and GC counters in the [partial paired benchmark log](bolt-performance-results-2026-07-22/windows-tuning-final-paired-partial.txt). The 5 MiB Bolt value comes from the [2 MiB crossover sweep](bolt-performance-results-2026-07-22/windows-tuning-threshold-2m.csv). These partial and mixed-run rows show the tuning direction, but they do not replace a complete paired certification run.

The 20 MiB result still uses a post-fix Bolt-only rerun against the earlier same-machine gRPC row. Approximate 95% mean intervals overlap: Bolt `[84.27, 92.63] ms`, gRPC `[87.76, 95.35] ms`.

## Focused Tuning Follow-up

The remaining weak cases were retested on 2026-07-23 without changing transport or networking. Production defaults retain only measured improvements:

- Auto-stream crossover moves from 1 MiB to 2 MiB. This keeps the 1 MiB case unary while 2 MiB serialized payloads continue to use the bounded stream path.
- The crossover is clamped below both the configured frame limit and the large-response envelope.
- Stream chunks remain 256 KiB. The fixed eight-chunk pipeline is expressed as a 2 MiB byte window so larger configured chunks cannot silently amplify queued memory.
- The batch ceiling remains 32 frames and 256 KiB. Tests at 64 and 128 frames were slower, so the experimental expansion was removed.

### Crossover Sweep

| Bolt-only payload | 2 MiB threshold mean / p95 | 5 MiB threshold mean / p95 | Decision |
|---:|---:|---:|---|
| 1 MiB | 8.350 / 10.801 ms | 7.420 / 10.917 ms | Both unary; variance control |
| 2 MiB | 12.906 / 15.279 ms | 16.275 / 19.318 ms | Stream at the 2 MiB serialized boundary |
| 5 MiB | 22.449 / 26.945 ms | 29.403 / 38.049 ms | Both stream; variance control |

The benchmark now writes input size, encoded Bolt payload size, configured threshold, and selected unary/stream mode into its raw log so future crossover evidence is explicit. The final paired subset completed 512 KiB and 1 MiB with Bolt means of `3.706 ms` and `7.689 ms`, versus gRPC means of `3.924 ms` and `8.337 ms`. Bolt then completed 2 MiB at `14.094 ms`, but the second gRPC 2 MiB launch stopped consuming CPU and never returned. The outer 30-minute timeout terminated the isolated run before 5 MiB and 20 MiB. These partial rows are directional only and are not a certification pass.

### Chunk And Batch Sweeps

| Scenario | Bolt mean / p95 | Decision |
|---|---:|---|
| 20 MiB, 256 KiB chunks, 2 MiB window | 88.449 / 110.708 ms | Retain |
| 20 MiB, 512 KiB chunks, 2 MiB window | 95.193 / 116.804 ms | Reject |
| 20 MiB, 1 MiB chunks, 2 MiB window | 115.653 / 140.621 ms | Reject |
| Concurrency 500, 32-frame batches | 6.235 / 8.301 ms | Retain |
| Concurrency 500, 64-frame batches | 6.283 / 9.318 ms | Reject |
| Concurrency 500, 128-frame batches | 6.728 / 9.048 ms | Reject |

## Before/After Direction

These deltas are directional only because the baseline used a shorter one-launch job.

| Bolt scenario | Before | Current | Delta |
|---|---:|---:|---:|
| Hub concurrency 64 mean | 3102.9 us | 650.9 us | -79.0% |
| Hub concurrency 64 p95 | 3663.9 us | 835.2 us | -77.2% |
| Hub concurrency 64 allocation | 395.07 KiB | 205.03 KiB | -48.1% |
| Direct concurrency 64 mean | 2240.7 us | 558.0 us | -75.1% |
| Hub batch throughput | 33,184 op/s | 58,883 op/s | +77.4% |
| Direct batch throughput | 41,661 op/s | 89,314 op/s | +114.4% |

## Memory And Trace Evidence

Managed allocation is lower for Bolt in every completed paired scenario. BenchmarkDotNet `MemoryDiagnoser` does not report peak working set or peak managed heap, so the peak-memory certification field remains missing.

A 15-second EventPipe trace was captured directly from the 1 MiB BenchmarkDotNet worker at:

`src/Tests/Bolt.Tests/BenchmarkDotNet.Artifacts/traces/bolt-payload-1m-windows-cpu-gc.nettrace`

The trace is ignored by Git because it is 51.6 MiB. Sampled stacks are predominantly asynchronous/thread-pool and socket waits. `Buffer.MemmoveInternal` accounts for about 0.95% exclusive samples and array allocation about 0.63%; the trace does not justify speculative dictionary or hash optimization. WPR was attempted first but system CPU profiling requires elevation.

A second 10-second EventPipe trace was captured from the concurrency-500 BenchmarkDotNet worker at:

`src/Tests/Bolt.Tests/BenchmarkDotNet.Artifacts/traces/bolt-concurrency-500-windows.nettrace`

It is also ignored by Git. Exclusive samples are dominated by thread-pool/socket/monitor waits. `BoltClient.HandleIncomingResponse` accounts for about 1.36%, array allocation about 1.87%, and `BoltClient.InvokeAsync` about 0.98%. There is no CPU hotspot supporting speculative hashing or collection changes; the remaining concurrency failure is primarily variance under this Windows/Hyper-V environment.

## Tests

- Focused crossover, pipeline ownership, and benchmark configuration after tuning: 34 passed.
- Complete non-PostgreSQL .NET suite after tuning: 461 passed, 7 Redis integration cases skipped by their existing environment guard.
- PostgreSQL Bolt authorization fixture: 4 passed through a loopback-only SSH/Tailscale tunnel to `xeon-dev` Docker 29.3.0. The tunnel was stopped and Testcontainers cleaned up.
- Browser build and lifecycle/parity suite: 18 passed.
- Repeated 20 MiB Hub regression: passed; no queue-capacity exception and accounting returned to zero.

## Raw Results

- [Direct and Hub CSV](./bolt-performance-results-2026-07-22/windows-direct-hub.csv)
- [Batch throughput CSV](./bolt-performance-results-2026-07-22/windows-batch-throughput.csv)
- [Sequential throughput CSV](./bolt-performance-results-2026-07-22/windows-sequential-throughput.csv)
- [Concurrency CSV](./bolt-performance-results-2026-07-22/windows-concurrency.csv)
- [Paired payload CSV before the Hub backpressure fix](./bolt-performance-results-2026-07-22/windows-payload-comparison-pre-backpressure-fix.csv)
- [Bolt payload CSV after the Hub backpressure fix](./bolt-performance-results-2026-07-22/windows-payload-bolt-post-backpressure-fix.csv)
- [Baseline direct and Hub CSV](./bolt-performance-results-2026-07-22/windows-baseline-direct-hub.csv)
- [Baseline batch-throughput CSV](./bolt-performance-results-2026-07-22/windows-baseline-batch-throughput.csv)
- [Baseline scalability CSV](./bolt-performance-results-2026-07-22/windows-baseline-scalability.csv)
- [2 MiB threshold sweep CSV](./bolt-performance-results-2026-07-22/windows-tuning-threshold-2m.csv)
- [5 MiB threshold sweep CSV](./bolt-performance-results-2026-07-22/windows-tuning-threshold-5m.csv)
- [512 KiB chunk sweep CSV](./bolt-performance-results-2026-07-22/windows-tuning-chunk-512k-window-2m.csv)
- [1 MiB chunk sweep CSV](./bolt-performance-results-2026-07-22/windows-tuning-chunk-1m-window-2m.csv)
- [Concurrency 500, 32-frame batch CSV](./bolt-performance-results-2026-07-22/windows-tuning-concurrency-500-batch-32.csv)
- [Concurrency 500, 64-frame batch CSV](./bolt-performance-results-2026-07-22/windows-tuning-concurrency-500-batch-64.csv)
- [Concurrency 500, 128-frame batch CSV](./bolt-performance-results-2026-07-22/windows-tuning-concurrency-500-batch-128.csv)
- [Final paired payload partial log](./bolt-performance-results-2026-07-22/windows-tuning-final-paired-partial.txt)

## Stop Decision

The focused tuning pass improved the unary/stream crossover and made the pipeline memory bound explicit, but it still does not satisfy the strict all-scenarios gate. Larger chunks and batches were measured and rejected. The next useful evidence would come from the same matrix on a repeatable non-Hyper-V Linux host; the current evidence does not support changing transport, adding TCP/QUIC RPC, or modifying production networking.

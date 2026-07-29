# Bolt Performance Optimization Investigation

Date: 2026-07-24 (Asia/Singapore)

## Scope

This investigation evaluates the current Bolt wire v2 WebSocket RPC client, protocol codec, and Hub against the remaining gaps in the Windows Bolt-versus-gRPC results. It focuses on CPU scheduling, allocations, buffer ownership, large-payload framing, and whether SIMD could provide a material gain.

No production code was changed during this investigation. Bolt Media, TLS, networking policy, Tailscale configuration, and alternative transports are outside this report.

Implementation and validation completed on 2026-07-25. See
[`bolt-performance-optimization-results-2026-07-25.md`](bolt-performance-optimization-results-2026-07-25.md)
for retained changes, rejected experiments, Windows/Linux benchmarks, limitations, and test results.

## Executive Conclusion

The remaining gap is narrow and is not caused by slow protocol parsing. Bolt already has lower managed allocation than gRPC in every completed paired scenario, usually by about 47% to 63%. The weak results are low-single-digit latency margins at concurrency 500, 512 KiB, 1 MiB, and 20 MiB, combined with benchmark variance and incomplete paired evidence.

The traces do not justify manual SIMD or an unsafe codec rewrite:

- On the 1 MiB trace, `Buffer.Memmove` was about 0.95% of exclusive samples and array allocation about 0.63%.
- On the concurrency-500 trace, async continuations, cancellation registration, task state, response completion, and lock contention were more visible than frame encoding.
- The process was already running .NET 10 x64 with AVX2. `Span.CopyTo` and `SequenceEqual` use runtime intrinsics where profitable.
- Batch encoding was about 0.02% of exclusive samples in the concurrency trace.

The highest-value work is therefore:

1. Remove deterministic large-frame fragmentation and ArrayPool bucket amplification.
2. Return Hub fragment-assembly buffers immediately after dispatch.
3. Reduce per-RPC and per-physical-write cancellation, task, logging, and completion overhead.
4. Stop allocating Hub capacity wake-up objects when no sender is waiting.
5. Remove the early large-stream chunk copy race.
6. Correct the benchmark topology and tail-latency measurements before making a strict certification claim.

These changes are plausible ways to turn the current directional wins into clear wins. The evidence does not support claiming that they will do so until the paired matrix is rerun.

## Current Evidence

### Remaining Weak Scenarios

| Scenario | Bolt mean / p95 | gRPC mean / p95 | Bolt allocation | gRPC allocation | Current interpretation |
|---|---:|---:|---:|---:|---|
| Concurrency 500 | 5.947 / 7.930 ms | 6.380 / 8.163 ms | 2037.17 KiB | 5523.16 KiB | Bolt point estimates are faster; mean confidence intervals overlap |
| 512 KiB | 3.706 / 4.649 ms | 3.924 / 4.880 ms | 549.96 KiB | 1141.42 KiB | Bolt point estimates are faster; mean confidence intervals overlap |
| 1 MiB | 7.689 / 9.824 ms | 8.337 / 10.299 ms | 1253.78 KiB | 2349.43 KiB | Bolt point estimates are faster; mean confidence intervals overlap |
| 2 MiB | 14.094 / 17.350 ms | Incomplete | 2962.75 KiB | Incomplete | No valid paired conclusion |
| 5 MiB | 22.449 / 26.945 ms | 25.779 / 31.588 ms | 5.11 MiB | 10.30 MiB | Directional only because results come from different runs |
| 20 MiB | 88.449 / 110.708 ms | 91.557 / 108.397 ms | 20.46 MiB | 41.27 MiB | Bolt mean is faster; p95 is about 2.1% slower; mixed-run evidence |

The full source results and environment are in [Bolt WebSocket Performance Certification](./bolt-performance-certification-2026-07-23.md).

### Trace Signals

The concurrency-500 trace shows several small costs rather than one dominant CPU hotspot:

| Sampled area | Approximate exclusive samples | Relevance |
|---|---:|---|
| Array allocation | 1.87% | Pool retention and avoidable temporary objects remain worth testing |
| Task state-machine boxing | 1.57% | Supports reducing Task conversion and async completion layers |
| Incoming response handling | 1.36% | Receive-loop scheduling can affect high-concurrency tails |
| Cancellation registration | 1.29% | Each RPC and physical write currently creates cancellation machinery |
| `BoltClient.InvokeAsync` | 0.98% | The aggregate RPC control path is measurable |
| Slow monitor entry | 0.58% | Connection selection and Hub byte accounting are candidates, not dominant hotspots |
| Hub pending-send release | 0.44% | Includes a lock and capacity signal replacement on every release |
| `TaskCompletionSource` construction | 0.40% | Consistent with avoidable Hub capacity signaling |
| Batch encoding | 0.02% | Does not support a batch codec rewrite |

These are sampled stacks, not exact wall-clock attribution. Each candidate still needs an isolated A/B benchmark.

## Findings And Recommended Actions

### P1: Return Hub Fragment Buffers Immediately

**Confidence: high. Primary impact: retained memory. Risk: low.**

The Hub rents `largeBuffer` when a WebSocket message spans receives, awaits `ProcessFrameAsync`, but does not return that buffer after successful dispatch. It is returned only when another fragmented message begins or when the connection closes.

Evidence: `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:338-441`. The client already performs the expected immediate return at `src/Libraries/Bolt/Bolt.Client/BoltClient.cs:1564-1567`.

With the current 256 KiB Hub receive buffer and growth heuristic, a 256 KiB stream payload plus its header can rent a roughly 1 MiB assembly buffer. A connection that becomes idle after that frame can retain the buffer unnecessarily.

**Recommended action:** return and clear `largeBuffer` immediately after the awaited frame dispatch, including exception-safe ownership handling. Add tests proving exactly-once return behavior after success, malformed frames, handler failure, cancellation, and disconnect.

### P1: Align Stream Chunks To Physical Frame Boundaries

**Confidence: high that the inefficiency exists; measured selection still required. Primary impact: large-payload latency and pooled memory. Risk: low when configured and tested.**

The default stream payload is 262,144 bytes. A `StreamData` frame adds a 21-byte Bolt header, producing a 262,165-byte WebSocket message:

`262,144 payload + 21 header = 262,165 bytes`

This is 21 bytes larger than the Hub's 256 KiB receive buffer, so fragmentation is deterministic. It also normally moves the outbound rented frame from a 256 KiB ArrayPool bucket into a 512 KiB bucket. With eight payload chunks in flight, the logical 2 MiB pipeline can hold about 4 MiB of physical frame arrays.

Evidence:

- `src/Libraries/Bolt/Bolt.Client/BoltClientOptions.cs:106-107`
- `src/Libraries/Bolt/Bolt.Protocol/Protocol/BoltCodec.cs:394`
- `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:337-399`
- `src/Libraries/Bolt/Bolt.Client/BoltClient.cs:1487-1544`

**Recommended action:** benchmark `64 KiB - 21`, `128 KiB - 21`, and `256 KiB - 21` payload chunks. Start with `256 KiB - 21` because it preserves nearly the current frame count while fitting the Hub receive boundary and the lower pool bucket. Do not promote a new default until 2 MiB and 20 MiB latency, CPU, allocation, peak heap, and working set all improve.

The slightly smaller chunk can add one frame for payloads that were exact multiples of 256 KiB. That tradeoff must be measured rather than assumed.

### P1: Remove Per-Write Cancellation Object Churn

**Confidence: high that allocations exist; performance gain requires A/B measurement. Primary impact: concurrency CPU and allocation. Risk: medium because timeout behavior is safety-critical.**

Both client and Hub send loops create a timeout `CancellationTokenSource` and a linked source for every physical write or batch:

- `src/Libraries/Bolt/Bolt.Client/BoltClient.cs:2367-2385`
- `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:4479-4489`

The configured timeout is positive in normal operation because zero falls back to the RPC or invocation timeout. This makes the cost unconditional. The Hub also converts every transport `ValueTask` to a `Task`, while the client already handles synchronously completed sends without that conversion.

**Recommended action:** prototype one connection-lifetime linked deadline source whose timer is armed only for the current physical write and disabled after completion. Keep the outer timeout wait because it protects buffer ownership when a transport ignores cancellation. Mirror the client's synchronous `ValueTask` fast path in the Hub.

Required invariants:

- Enqueue and overall RPC deadlines remain active.
- A physical send timeout retires the connection.
- Caller cancellation after enqueue does not return a buffer while the transport may still use it.
- Every pending buffer and completion is released exactly once on success, timeout, cancellation, failure, and disconnect.

### P1: Reduce Unary RPC Control-Path Allocations

**Confidence: high that the objects are created; individual gains are likely small but cumulative at concurrency 500. Primary impact: high-concurrency mean and tail. Risk: low to medium.**

The normal unary path currently:

- Converts the pooled response `ValueTask` to a `Task` with `GetTask().AsTask()`.
- Creates a timeout source and a linked source for each call.
- Allocates a `Stopwatch` for each call.
- Uses dynamic-level `ILogger.Log`, which constructs and boxes the parameter array even when debug logging is disabled.
- Waits for a positive physical-send completion before awaiting the already-live response.
- Retains only 256 pooled RPC calls and 256 pooled reliable-send completions, below the measured burst of 500.

Evidence: `src/Libraries/Bolt/Bolt.Client/BoltClient.cs:824-900`, `src/Libraries/Bolt/Bolt.Client/PooledRpcCall.cs:16`, and `src/Libraries/Bolt/Bolt.Client/PooledSendCompletion.cs:8`.

**Recommended action, in order:**

1. Replace the two RPC cancellation sources with one deadline source, preserving caller cancellation and timeout classification.
2. Guard timing and logging with `IsEnabled`, use timestamp values instead of allocated `Stopwatch`, and use source-generated/static logging methods.
3. A/B test a bounded pool capacity of 1,024 for RPC calls and send completions. Record the retained-memory cost as well as allocation reduction.
4. Consume the pooled response `ValueTask` without converting it to a `Task`, with tests proving it is consumed exactly once on all error paths.
5. Prototype an enqueue completion that reports only physical-send failure into the pending RPC. A successful response already proves the request was sent, so the normal RPC path may not need to await a separate positive send completion. Keep this only if transport failures still complete the RPC promptly and ownership tests remain intact.

The fifth item has the highest behavioral risk and should not be combined with the earlier changes in one benchmark run.

### P1: Make Hub Capacity Signaling Lazy

**Confidence: high. Primary impact: concurrency allocation and lock hold time. Risk: low.**

`ReleasePendingBytes` creates a new `TaskCompletionSource` on every release, even when no producer is waiting for byte capacity. This appears in the concurrency trace.

Evidence: `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:4850-4898`.

**Recommended action:** keep the existing lock and create a capacity signal only when a producer actually encounters a full byte budget. A release should take and complete the signal only when one exists. This is simpler and safer than immediately replacing the accounting with a lock-free algorithm.

Reprofile after this change. Consider an atomic reservation fast path only if the lock remains material and stress tests can prove there are no lost wake-ups or byte-accounting errors.

### P2: Eliminate Early Large-Stream Chunk Copies

**Confidence: medium. Primary impact: variable large-RPC allocation and scheduling. Risk: medium.**

Incoming stream handlers start through `Task.Run`. The built-in large-RPC handler reads its metadata header, reserves the final buffer, and only then installs its direct inbound sink. Payload chunks that arrive before that sink is installed are copied into new managed arrays so the receive buffer can be reused.

Evidence:

- `src/Libraries/Bolt/Bolt.Client/BoltClient.cs:459-541`
- `src/Libraries/Bolt/Bolt.Client/BoltClient.cs:1723-1755`
- `src/Libraries/Bolt/Bolt.Client/BoltStream.cs:213-246`

The behavior plausibly explains approximately 1.2 to 1.4 MiB of allocation variation seen in some 2 MiB launches, but the existing trace does not prove the exact amount.

**Recommended action:** specialize admission for the two internal large-RPC stream commands so a stateful collector owns incoming chunks from the first metadata frame. Do not change general user stream semantics. Validate concurrent opens, malformed headers, early close, cancellation, budget rejection, and handler failure.

### P2: Lower Per-Connection Hub Receive Memory Carefully

**Confidence: high that memory is retained; the best default is not yet measured. Primary impact: Hub working set at connection scale. Risk: medium because smaller buffers can increase receive and copy work.**

The Hub currently rents a fixed 256 KiB base receive buffer for every active connection. That is about 250 MiB for 1,000 connections before other connection state and fragment buffers. BenchmarkDotNet allocation columns do not expose this steady-state pooled working set.

Evidence: `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:337`.

**Recommended action:** make the Hub base receive size configurable and test 64, 128, and 256 KiB at 100, 500, and 1,000 idle and active connections. Measure working set, managed heap, pool retention, receive-call count, CPU, and latency. Pair this experiment with the aligned chunk sweep; changing either value independently can give a misleading result.

Do not simply increase the client receive buffer from 64 KiB to 256 KiB. That can improve a two-client benchmark while adding roughly 192 MiB per 1,000 connected clients.

### P2: Reduce Connection Selection And Receive-Loop Scheduling Cost

**Confidence: medium. Primary impact: concurrency tails. Risk: low for lookup consolidation, medium for continuation changes.**

Each normal invocation first locks to evaluate `IsConnected`, then locks and scans again in `GetConnection`. At high load, response completion can also run the awaiting RPC continuation inline on the receive loop because `PooledRpcCall` does not enable asynchronous continuations.

Evidence: `src/Libraries/Bolt/Bolt.Client/BoltClient.cs:92-97`, `src/Libraries/Bolt/Bolt.Client/BoltClient.cs:827-840`, `src/Libraries/Bolt/Bolt.Client/BoltClient.cs:1462-1480`, and `src/Libraries/Bolt/Bolt.Client/PooledRpcCall.cs`.

**Recommended action:** first combine registration, availability, and connection selection into one lookup. If contention remains, A/B test an immutable or volatile connection snapshot updated only when connections change. Separately A/B test asynchronous RPC continuations at concurrency 500 and 1,024. Async continuations may improve tail latency by protecting the receive loop, but they can worsen mean latency through ThreadPool scheduling, so this must not be changed blindly.

### P3: Optional Owned Response API

**Confidence: high about the ownership tradeoff; low priority because Bolt already wins allocation. Primary impact: large unary response allocation. Risk: high for API misuse and retained pooled memory.**

The current unary API returns `ReadOnlyMemory<byte>` that remains valid after the receive callback, so Bolt must materialize an owned managed array. Replacing that storage with pooled memory in the existing API would be unsafe because callers have no disposal contract.

An opt-in disposable response API could remove a payload-sized managed allocation for advanced callers. It should not replace the current API. A 20 MiB response can occupy a 32 MiB ArrayPool bucket until disposal, so incorrect use can make retained memory worse than the current exact-sized array.

**Recommended action:** defer this API until the P1/P2 work is measured. If real service profiles show response materialization as a material cost, prototype an explicit `IMemoryOwner<byte>` response with analyzable disposal tests and retained-pool measurements.

## SIMD Assessment

| Candidate | Evidence | Decision |
|---|---|---|
| Frame/header parsing | Headers are small fixed fields using `BinaryPrimitives`; no codec hotspot appears | Do not add manual SIMD |
| Payload copies | `Buffer.Memmove` is below 1% in the 1 MiB trace and is already runtime-vectorized | Fix ownership and fragmentation first |
| Equality checks | `SequenceEqual` is negligible and runtime-vectorized | No change |
| FNV-1a hashing | Hashes are cached, the recurrence is serial, and no hash hotspot appears | No change |
| WebSocket masking | About 0.01% in the sampled trace and handled by the runtime | No change |
| Batch validation/encoding | Batch encoding is about 0.02% of exclusive samples | No batch codec rewrite |

Capture a dedicated 20 MiB CPU and GC trace after frame alignment. Reconsider custom vectorization only if a Bolt-owned compute loop consumes at least about 5% exclusive CPU and the runtime-generated code is demonstrably not vectorized. Current evidence is far below that threshold.

## Benchmark Issues To Correct Before Certification

### Payload Topology Is Not Equivalent

The current payload benchmark sends Bolt through caller -> Hub -> service -> Hub -> caller, while gRPC calls its backend directly. This disadvantages Bolt, so the directional result is encouraging, but it does not isolate protocol efficiency.

Evidence: `src/Tests/Bolt.Tests/PayloadBenchmarks.cs:101-146`.

Keep the existing end-to-end comparison, but add separately labeled matched fixtures:

- Direct Bolt versus direct gRPC for protocol cost.
- Routed Bolt versus a comparably routed gRPC proxy for Hub cost.

### Current p95 Is Not Per-Request p95

`StatisticColumn.P95` is calculated from BenchmarkDotNet measurement samples, which are normalized iteration measurements containing multiple operations. It is not the distribution of every individual request. It smooths the tail and cannot certify production request p95 or p99.

Keep BenchmarkDotNet for means, confidence intervals, and allocations. Add a dedicated latency harness that records each request with low-overhead timestamps and reports p50, p95, p99, and maximum latency under concurrency.

### The gRPC 2 MiB Stall Is Undiagnosed

The partial run completed one gRPC launch and stopped during the second without an exception. An earlier run completed 2 MiB. The benchmark gives the gRPC call no deadline and discards the Kestrel lifetime task, so the current evidence cannot distinguish HTTP/2 I/O, process lifecycle, GC pressure, or another transient.

Evidence: `src/Tests/Bolt.Tests/PayloadBenchmarks.cs:82-156`, `src/Tests/Bolt.Tests/PayloadBenchmarks.cs:212-232`, and `windows-tuning-final-paired-partial.txt`.

Run gRPC 2 MiB in isolation with an external watchdog that captures a dump, runtime counters, and a nettrace before termination. Retain and observe server lifetime tasks, use isolated ports, and apply comparable logical deadlines to both transports. Do not count the stall as a Bolt performance win.

### Control Run Variance

Run baseline and candidate in randomized process blocks rather than all baseline runs followed by all candidate runs. Record every effective environment variable, chunk size, receive size, batch limit, timeout, runtime version, GC mode, and CPU state in the artifact. Windows under Hyper-V remains useful for local A/B work, but final confidence should be checked on the same stable Linux host as planned.

## Recommended Implementation And Measurement Order

### Pass 1: Benchmark Integrity

1. Add matched topology labels and a true request-latency harness.
2. Add the gRPC watchdog and observable server lifecycle.
3. Preserve the current raw results as the baseline.

### Pass 2: Low-Risk Allocation And Lifetime Fixes

1. Return the Hub fragment buffer immediately.
2. Make Hub capacity signaling lazy.
3. Guard RPC diagnostics and remove the allocated `Stopwatch` on the disabled path.
4. Consolidate the duplicate client connection lookup.
5. Mirror the client transport `ValueTask` fast path in the Hub.

Measure each change independently at concurrency 500 before combining them.

### Pass 3: Framing And Cancellation Experiments

1. Sweep aligned stream chunks at 64, 128, and 256 KiB boundaries.
2. Sweep Hub receive buffers with connection-scale working-set measurements.
3. Prototype reusable physical-write deadline state.
4. Increase completion pool retention only if the 500/1,024 burst A/B result pays for the retained objects.

### Pass 4: Scheduling And Ownership Prototypes

1. Install the built-in large-RPC collector before payload chunks can be copied.
2. Test direct pooled `ValueTask` consumption.
3. Test failure-only send completion for unary RPCs.
4. Test asynchronous response continuations.

Only retain candidates that improve both the target scenario and the memory profile without weakening reliability.

## Minimum Validation Matrix

Correctness coverage must include blocked enqueue, physical send timeout, caller cancellation before and after enqueue, request cancellation, transport failure, malformed stream metadata, early stream close, Hub disconnect, and exactly-once pooled-buffer release.

Performance coverage:

| Area | Cases |
|---|---|
| Concurrency | 10, 100, 256, 257, 500, and 1,024 calls on one connection |
| Payload | 512 KiB, 1 MiB, 2 MiB, 5 MiB, 20 MiB, and 32 MiB |
| Chunk payload | 65,515; 131,051; 262,123; and current 262,144 bytes |
| Hub receive buffer | 64, 128, and 256 KiB |
| Connection scale | 100, 500, and 1,000 idle and active Hub connections |
| Repetitions | At least three randomized baseline/candidate process blocks |

Record mean, 95% confidence interval, true request p95/p99, throughput, allocated bytes, peak managed heap, working set, Gen2 collections, ArrayPool retention, CPU time, receive fragments, queue wait, batch size, and physical-write duration.

## Explicit Non-Actions

The current evidence does not justify:

- Manual SIMD, unsafe parsing, or custom memory-copy routines.
- Replacing FNV hashing or the pending-call dictionaries.
- Increasing batch limits above 32 frames or 256 KiB; previous larger batches were slower.
- Increasing chunks to 512 KiB or 1 MiB; previous tests were slower and amplified buffers.
- Making pooled/disposable responses the default API.
- Adding TCP, QUIC RPC, compression, TLS, or networking changes.
- Changing Bolt Media as part of this RPC optimization pass.

## Decision

Proceed with the targeted P1 work and measured framing experiments before considering deeper protocol changes. Bolt's managed allocation position is already strong. The best opportunity to improve both speed and memory is to reduce control-path object churn and align physical frame ownership with actual buffer boundaries, not to add SIMD.

After those changes, rerun the complete paired matrix. A strict claim that Bolt beats gRPC still requires complete matched runs, non-overlapping confidence intervals, true request-level tail latency, and peak-memory evidence.

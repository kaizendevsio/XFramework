---
title: "Yap call benchmark: current recovery, picture profiles and comparable metrics"
date: 2026-10-08
category: performance-issues
module: Bolt.Media
problem_type: benchmark_methodology
component: call_media_benchmark
severity: medium
tags: [yap, bolt, benchmark, recovery, audio, video]
---

# Call media benchmark methodology and evidence

The October 7 benchmark used only 640×360 and a recovery port that predated the shipped slow-keyframe and
uplink-repair fixes. Its freeze threshold changed with the path's achieved FPS and omitted initial/trailing
stalls. Packet delivery mixed sender/receiver window boundaries. Those measurements cannot establish current
production audio quality, current recovery behavior or performance at 1080p30.

The updated [benchmark workflow](../../../.github/workflows/call-media-benchmark.yml) and
[methodology](../../../src/Tests/Bolt.Rtc.IntegrationTests/bench/README.md) compare the same metric harness
against baseline `8ae24751` and the updated production snapshot. Three picture profiles (360p30, 720p30,
1080p30) run through clean 20 Mbit/s, 512 kbit/s / 500 ms / 1% loss and 512 kbit/s / 1000 ms / 3% loss
receiver links. A separate production WASM/pacer/relay experiment uses the existing synthetic-codec browser
call test; its results are explicitly separate from the native/real-codec model.
The severe production experiment shapes A's downlink to 512 kbit/s, 1000 ms round trip and 3% loss, with delay
but no injected loss on A's uplink. It measures constrained-downlink receiver recovery, redundancy and pacing.
It cannot alone validate repairs for sender-uplink loss; the real `MediaTransportClient`/relay transport-feedback
regression tests are the direct evidence for the RTT-aware uplink repair change.

Recovery parity compares actual C# source decisions and reassembled bytes against the JavaScript model on
every deterministic trace step. It includes the current independent-keyframe supersession rule. Metric tests
cover fixed-threshold freezes at both window edges, entirely stalled video, sender audio cohorts, late arrivals
and duplicates. Focused local validation on October 8 passed all six parity cases and three metric tests.
The identical harness also compiled against a temporary archive of baseline
`8ae24751d0975c6dbb8e2a368059aa2338e54df3` and passed all six parity cases. Its actual buffer correctly selected
the earlier keyframe recovery behavior; current selected independent-keyframe supersession.
`actionlint` 1.7.12, shell syntax checks and Python report compilation also passed.

Three local direct-loopback smoke runs verified actual H.264 at 1920×1080 / 30 FPS / 4000 kbps video, Chrome
154.0.8037.57, 2-second warmup, 5-second measurement, 1-second drain. Current runs used uncommitted source;
baseline used its archived production assemblies/browser assets with the same metric harness. These are smoke
checks, not TURN/netem measurements or evidence of statistically reliable differences:

| path | decoded FPS | video delay p50 / p99 ms | video samples / barcode success | longest frame gap ms | audio delivery | audio transport p50 / p99 ms | audio samples |
|---|---|---|---|---|---|---|---|
| native WebRTC | 29.6 | 23 / 35 | 148 / 100% | 94 | 100% | 0 / 8 | 250 |
| modeled Bolt current | 29.8 | 12 / 19 | 149 / 100% | 64 | 100% | 1 / 9 | 249 |
| modeled Bolt baseline 8ae24751 | 29.6 | 11 / 20 | 148 / 100% | 68 | 100% | 1 / 10 | 249 |

All smoke runs reported zero freezes at the fixed 250 ms threshold. Raw results are retained in
[native smoke JSON](yap-call-media-benchmark-2026-10-08/native-1080p30-smoke.json) and
[Bolt current smoke JSON](yap-call-media-benchmark-2026-10-08/bolt-1080p30-smoke.json) and
[Bolt baseline smoke JSON](yap-call-media-benchmark-2026-10-08/bolt-baseline-1080p30-smoke.json). These prove that timestamps,
barcodes, delivery cohorts and codec setup function at 1080p30. They do not prove bandwidth superiority,
severe-link audio recovery, real-device heating improvements or production pacing effectiveness.

The [October 8 GitHub run and raw results](yap-call-media-benchmark-2026-10-08/run-37779802662/README.md)
retain three 60-second repeats per profile against full baseline/current revisions. All 81 modeled runs completed.
Clean 1080p30 delivered 30 FPS and 100% audio on every path, but current modeled severe 1080p30 remained at
1.3–1.8 FPS with audio p99 above 10 seconds. Severe 720p30 rendered fewer frames than baseline. The model
does not establish that production audio/pacing changes cure these regressions.

Production baseline severe calls opened UDP, then fell back to WebSocket during warmup and stayed there
through measurement. WebSocket bypasses the shaped TURN path; those later throughput/delay numbers are
invalid constrained-path performance evidence. All three still failed the existing lifetime audio delivery assertion
(93.63–95.62% A->B). Production video window metrics must be distinguished from lifetime audio/recovery counters,
which include startup and warmup. Current severe production also fell back in every repeat and failed lifetime
audio delivery (91.47–95.66% A->B). One repeat retried UDP during measurement, with 25–33 second frame gaps
and three B->A decoder resets. Both snapshots passed clean production. The run establishes unresolved severe
quality/fallback problems; it does not establish a production audio cure or thermal improvement.

The [production-only probe/drain follow-up](yap-call-media-benchmark-2026-10-08/run-37784214293/README.md)
measured `d258a01a1368093d93a454d713d475359b9b8375` in another twelve 60-second calls. Clean production
again passed. Severe current still fell back in every repeat, failed lifetime A->B audio (94.55–94.85%), and
two repeats had 12–13 second measured A->B gaps plus two B->A decoder resets each. Probe waits became longer
but first sampled fallback timing remained 18.9–19.0 seconds. This follow-up precedes the service rate-loop
probe-gating correction. Its raw measurements and startup/path samples remain separate from the initial run;
different loss traces and fallback timing prevent causal comparisons of their ranges.

The [final production transport run](yap-call-media-benchmark-2026-10-08/run-37786644195/README.md) measured
`d043c71a03ba8f1b0967f68a68ef978cc9eaf267` after service feedback during probes and empty/sparse probe guards.
All twelve calls completed. Clean current passed at 30 FPS and 100% audio. Severe current still switched A to
WebSocket before the measurement window in every repeat and failed lifetime A->B audio (93.90–95.56%).
Its settled socket windows had 99–156 ms gaps and no decoder resets, but neither direction represents a valid
complete shaped-UDP window. No severe audio cure or thermal improvement is established. The final report
retains startup probe verdicts, path timing and approximate recovery deltas separately from lifetime totals.

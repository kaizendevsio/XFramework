# Final production transport measurement

[Workflow run 37786644195](https://github.com/kaizendevsio/XFramework/actions/runs/37786644195) measured current `d043c71a03ba8f1b0967f68a68ef978cc9eaf267` against baseline `8ae24751d0975c6dbb8e2a368059aa2338e54df3`: clean/severe production only, 60 seconds, three repeats per snapshot/profile. All four builds and 12 calls produced complete artifacts. The modeled matrix was intentionally skipped (`-f modeled=false -f production=true`). Later tenant UI commit `049fc41f` has no changes under `src/Libraries/Bolt`; this is the final intended Bolt transport source measured in this task.

This source includes the probe/drain follow-up plus service feedback/rate ticks during probing and the guard against declaring capacity from empty/sparse probe steps. The [initial run](../run-37779802662/README.md) and [probe/drain-only run](../run-37784214293/README.md) remain separate and unchanged. Hosted runners, random loss and fallback timing differ: do not treat their gap ranges as causal comparisons.

The [per-repeat production table](production-report.md) retains final paths and quality failures. Workflow success means builds and measurement JSON completed; the runner deliberately preserves results when quality assertions fail. It does not mean all quality assertions passed.

## Final observed quality

| profile / snapshot | direction | measured rendered FPS range | measured longest gap ms range | decoder reset delta range | lifetime audio delivery % range | lifetime audio p50 / p99 ms ranges |
|---|---|---|---|---|---|---|
| clean-1080p30 / baseline | A->B | 30-30 | 163-196 | 0-0 | 100-100 | 35-36 / 62-90 |
| clean-1080p30 / baseline | B->A | 29.9-30 | 166-213 | 0-0 | 100-100 | 35-37 / 66-81 |
| clean-1080p30 / current | A->B | 30-30 | 153-180 | 0-0 | 100-100 | 33-34 / 59-61 |
| clean-1080p30 / current | B->A | 30-30 | 146-163 | 0-0 | 100-100 | 34-35 / 63-82 |
| 512k-1000ms-3pct / baseline | A->B | 17.4-26.2 | 156-7587 | 0-1 | 94.03-94.52 | 20-22 / 266-449 |
| 512k-1000ms-3pct / baseline | B->A | 21.8-30 | 109-142 | 0-0 | 98.98-99.71 | 19-19 / 2060-2323 |
| 512k-1000ms-3pct / current | A->B | 21-30 | 124-156 | 0-0 | 93.9-95.56 | 19-21 / 178-298 |
| 512k-1000ms-3pct / current | B->A | 20-25 | 99-138 | 0-0 | 98.14-98.35 | 19-21 / 1850-2064 |

Clean production passes every existing assertion on both snapshots: current 30 FPS and 100% audio with no resets; baseline 29.9-30 FPS and 100% audio with no resets. All sampled clean open/warmup/measure paths remain UDP for both peers. Current longest frame gaps are 146-180 ms versus baseline 163-213 ms. This demonstrates a passing clean-path smoke/performance check on the measured source, not a statistically established general latency gain.

Severe current still fails A->B lifetime audio delivery in all three repeats: 95.56%, 95.56%, 93.90% against the 98% assertion. Baseline severe also fails every A->B lifetime audio assertion (94.03-94.52%). Current measured windows have 21-30 FPS A->B and 20-25 FPS B->A, 99-156 ms gaps and no decoder resets, but they all run after A has settled on WebSocket. Baseline repeat 2 has a measured 7.587-second A->B gap and one decoder reset while its path retries UDP into measurement. Mixed transport/loss timing prevents interpreting the current absence of a large gap as a causal cure.

**The severe shaped-UDP quality question remains unresolved.** All current and baseline severe calls end with A on WebSocket and B on UDP. WebSocket bypasses TURN/netem. Neither direction supplies valid complete-window shaped-UDP throughput or delay evidence. The measured final source does not establish a severe audio cure, production bandwidth advantage, real codec CPU reduction or thermal improvement.

## Startup probe and fallback evidence

[Current severe timeline](current-severe-timeline.json) and [baseline severe timeline](baseline-severe-timeline.json) preserve 0.5-second start samples and open/warmup/measure path, rate, send and receive snapshots. Recent-picture strings are omitted. [Current clean](current-clean-timeline.json) and [baseline clean](baseline-clean-timeline.json) samples establish the observed clean UDP paths; sampling cannot exclude transitions between samples.

Current A initially opens UDP at the 5.7-5.8-second sample, first appears on stalled WebSocket at 18.9-19.0 seconds, is negotiating at about 39 seconds, retries UDP at 49.0-49.1 seconds, and reaches flapping WebSocket by the final warmup sample at 54.0-54.1 seconds. Every measured current path sample is WebSocket for A and UDP for B. Current lifetime audio loss includes startup/warmup; no production measured-send audio cohort exists to isolate later socket-window delivery.

Baseline first sampled stalled WebSocket is 18.5-18.9 seconds. Repeat 2 retries UDP at the first measurement sample (59.1 seconds) and returns to flapping WebSocket at 64.1 seconds. Repeats 1/3 settle on WebSocket before measurement. This difference in transport histories explains why steady-window comparisons must remain descriptive rather than causal.

All three current A startup probe verdicts select default 884 kbit/s with no usable reports after 2319-2373 ms. Repeats 1/2 retain RTT readings of 1037/2267 ms and sparse echoes of 1/3 and 1/1, without asserting offered capacity. By comparison, baseline repeat 2 declares at least 600 kbit/s from a zero-of-zero packet span after 205 ms and chooses 570 kbit/s startup. Raw senderProbe strings preserve both outcomes. The new guard is consistent with refusing phantom capacity in these observations; it has not prevented fallback or established severe quality success.

Current A per-repeat maxima across the start samples are total controller 8888/9442/6206 kbit/s and pacer sent 5476/7361/3722 kbit/s. Baseline is 9390/795/9250 and 6279/684/6124 respectively. These are sampled maxima, not integrated wire rates. High startup samples and fallback remain visible despite the verified service feedback/probe fixes.

## Lifetime and measurement boundaries

Video FPS, longest frame gap and decoder reset deltas refer to the measured 60 seconds. Production audio delivery/delay and receive recovery totals span the call lifetime including startup/warmup. Audio delivery spans received sequence numbers and omits unseen leading/trailing loss; it is not comparable to the model's fixed sender cohort or audible playout delay.

Approximate recovery deltas below subtract the last warmup sample from the final sample. That sampled boundary slightly precedes the actual video reset; it is not an exact measured-window counter. N/R/A/I means NACK requests / recovered fragments / abandoned fragments / incomplete pictures. Empty or unchanged counters do not demonstrate better shaped recovery when the path has moved to WebSocket.

| snapshot / repeat | receiver | last warmup to final seconds | N/R/A/I deltas |
|---|---|---|---|
| baseline r1 | A | 53.6-113.8 | 0/0/0/0 |
| baseline r1 | B | 53.6-113.8 | 0/0/0/1 |
| baseline r2 | A | 54.1-114.3 | 0/0/0/0 |
| baseline r2 | B | 54.1-114.3 | 162/3/53/1 |
| baseline r3 | A | 54.1-114.3 | 0/0/0/0 |
| baseline r3 | B | 54.1-114.4 | 0/0/0/0 |
| current r1 | A | 54.0-114.2 | 0/0/0/0 |
| current r1 | B | 54.0-114.2 | 0/0/0/0 |
| current r2 | A | 54.1-114.3 | 0/0/0/0 |
| current r2 | B | 54.1-114.3 | 0/0/0/0 |
| current r3 | A | 54.1-114.3 | 0/0/0/0 |
| current r3 | B | 54.1-114.3 | 0/0/0/0 |

Severe shaping applies 512 kbit/s, 500 ms one-way delay and 3% loss only to A downlink, with 500 ms delay and no injected loss on A uplink. It cannot directly validate sender-uplink loss repairs. The browser test waits at most 45 seconds for datagram paths, then continues even on WebSocket. Codec payloads and decoder are synthetic, so this experiment measures production transport behavior rather than real encoder/decoder quality, CPU, battery or heat. Full revision files and all original production JSON/quality failure files are retained alongside this report.

# Production-only follow-up: probe wait and relay repair drain accounting

[Workflow run 37784214293](https://github.com/kaizendevsio/XFramework/actions/runs/37784214293) measured current `d258a01a1368093d93a454d713d475359b9b8375` against baseline `8ae24751d0975c6dbb8e2a368059aa2338e54df3`: clean/severe production only, 60 seconds, three repeats per snapshot/profile. All builds and 12 completed calls produced artifacts. The modeled matrix was intentionally skipped using `-f modeled=false -f production=true`.

This source adds a longer RTT-aware startup probe wait and accounts successful relay repairs in drain progress. It precedes the subsequent service rate-loop/probe-gating fix. Keep this evidence distinct from the [initial run](../run-37779802662/README.md); independent runner/loss/transport-switch timing prevents causal comparisons of their gap ranges.

The [per-repeat production table](production-report.md) preserves quality failures and final paths. The workflow succeeds after completed measurement JSON even when quality assertions fail. Green workflow status establishes completed setup/measurement, not successful severe quality.

## Observed quality and validity

| profile / snapshot | direction | measured rendered FPS range | measured longest gap ms range | decoder reset delta range | lifetime audio delivery % range | lifetime audio p50 / p99 ms ranges |
|---|---|---|---|---|---|---|
| clean-1080p30 / baseline | A->B | 29.9-30 | 169-187 | 0-0 | 100-100 | 35-35 / 60-77 |
| clean-1080p30 / baseline | B->A | 30-30 | 172-182 | 0-0 | 100-100 | 34-36 / 73-86 |
| clean-1080p30 / current | A->B | 30-30 | 135-149 | 0-0 | 100-100 | 33-34 / 61-63 |
| clean-1080p30 / current | B->A | 30-30 | 152-163 | 0-0 | 100-100 | 34-34 / 63-72 |
| 512k-1000ms-3pct / baseline | A->B | 17.4-28.1 | 144-13419 | 0-0 | 94.72-95.01 | 19-20 / 232-427 |
| 512k-1000ms-3pct / baseline | B->A | 24.9-30 | 119-1588 | 0-2 | 97.23-99.5 | 18-20 / 63-2247 |
| 512k-1000ms-3pct / current | A->B | 10.7-29.2 | 145-13124 | 0-0 | 94.55-94.85 | 20-21 / 228-415 |
| 512k-1000ms-3pct / current | B->A | 22.2-30 | 94-1510 | 0-2 | 98.14-99.57 | 20-20 / 78-2252 |

Clean production passed existing assertions for all calls: current 30 FPS and 100% audio with no resets; baseline 29.9-30 FPS and 100% audio with no resets. All sampled clean open/warmup/measure paths remain UDP for both peers. Clean results show no quality regression in this run; three repeats do not establish a general latency/efficiency gain.

Severe current still fails A->B lifetime audio in every repeat (94.55-94.85%). Repeats 1/2 also fail measured video gaps: A->B 13.124/12.427 seconds, B->A 1.006/1.510 seconds, and two B->A decoder resets each. Baseline severe also fails A->B lifetime audio in all repeats (94.72-95.01%); repeats 1/2 have A->B 12.642/13.419-second gaps, B->A 1.588/1.258-second gaps and two/one B->A resets. All severe calls end with A on WebSocket and B on UDP. These are unresolved measured quality failures. No severe audio cure is demonstrated.

**Metric scopes:** sent/rendered FPS, frame gaps and reset deltas refer to the measured 60-second window. Audio delivery/delay and receive recovery totals cover the call lifetime including startup/warmup. Audio uses received sequence span, omitting completely unseen leading/trailing packets. Production audio is not the modeled sender cohort and does not measure audible playout.

**Transport validity:** WebSocket bypasses shaped TURN/netem. Both severe snapshots have mixed/fallback transport, so neither supplies a valid complete-window shaped-UDP performance result. The test continues if its 45-second UDP wait times out; path sampling cannot prove continuous UDP between samples. Severe loss is injected only on A downlink (512 kbit/s, 500 ms one-way delay, 3% loss); A uplink receives delay without injected loss. This experiment cannot directly verify sender-uplink-loss repair fixes. Synthetic codecs/decoder do not measure real-device CPU, heat or battery.

## Startup probe and fallback timing

[Baseline severe samples](baseline-severe-timeline.json) and [current severe samples](current-severe-timeline.json) retain the 0.5-second start samples plus open/warmup/measure path/rate/send/recovery samples. Recent-picture strings are omitted. The last warmup sample is an approximate counter boundary, not an exact reset timestamp. Clean samples are retained in [baseline clean timeline](baseline-clean-timeline.json) and [current clean timeline](current-clean-timeline.json).

Both severe snapshots initially show A UDP at 5.7-5.8 seconds, then A stalled WebSocket at 18.9-19.0 seconds. Repeats 1/2 on both snapshots retry UDP at about 34 seconds, are stalled again at about 39 seconds, negotiating at 49 seconds, and flapping WebSocket at 64.0-64.2 seconds during measurement (which starts at about 54 seconds). Repeat 3 on both snapshots retries UDP at about 44 seconds and is flapping WebSocket by 49 seconds. The new wait has not visibly prevented this fallback pattern in these samples.

Current A probe verdict in repeats 1/2 remains no reports/default 884 kbit/s, but elapsed probe duration is 2347-2387 ms versus baseline 1112-1117 ms. Current repeat 3 declares at least 1500 kbit/s uplink and 600 kbit/s downlink with 1059 ms RTT, while its 1500 stage reports zero delivered rate and a zero-of-zero packet span; it selects a 1425 kbit/s startup budget. Raw senderProbe strings retain this inconsistency for diagnosis. Baseline probes in this run all choose default 884 kbit/s with no reports. B consistently selects 7125 kbit/s from a low-RTT uplink probe.

Across current A's 0.5-second start samples, per-repeat maximum controller total is 9016-9553 kbit/s and per-repeat maximum pacer sent rate is 5657-6316 kbit/s. Baseline maxima are 8972-9188 and 5939-6388 kbit/s respectively. These are sample maxima rather than integrated wire measurements; they show startup remains high even when the final stored probe falls back to its default. Subsequent rate-loop changes require separate measurements before claims of improvement.

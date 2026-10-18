# GitHub TURN/netem measurements â€” October 8, 2026

[Workflow run 37779802662](https://github.com/kaizendevsio/XFramework/actions/runs/37779802662): 60 seconds per measurement, three repeats per path/profile.
Baseline source `8ae24751d0975c6dbb8e2a368059aa2338e54df3`; current source `4a41da2e3711f39d36484938dc2a5dd0bdbf6b34`.
This is the initial comparison, before follow-up fixes for startup probe timing and relay repair drain accounting.
Preserve these results; evaluate those fixes in a separately revision-labeled production-only rerun.
The later HEVC preflight commit is outside this measurement. The native comparator uses the current measurement harness. Chrome 154.0.8037.97.

Values below are median (minimumâ€“maximum) across three repeats, rounded only for presentation. Quantiles are per-repeat quantiles, not pooled quantiles.
Raw JSON and scenario/revision files are retained in the adjacent artifact folders. Repeats alternate execution order; independent random loss means the runs are not paired loss traces.

## Modeled real-codec browser paths

This exercises actual snapshot rate-controller decisions and parity-validated recovery, real H.264/Opus and TURN shaping. It excludes production media service/pacer/relay lanes/audio redundancy.
The camera profile stays at the named size; production tier adaptation is evaluated separately. Every run uses a fixed 250 ms freeze threshold, window boundaries and a five-second audio cohort drain.
Delay measures transport/decoded video timestamps, excluding audible playout. Barcode sample coverage is available in each raw result.

| link/profile | path | decoded FPS | audio delivery % | audio p50 / p99 ms | video p50 / p99 ms | freeze excess seconds | sender wire / receiver reverse kbps |
|---|---|---|---|---|---|---|---|
| 512k-1000ms-3pct-1080p30 | bolt-baseline | 1.7 (1.3â€“1.8) | 97.7 (96.1â€“98) | 1637 (1435â€“1798) / 8722 (8326â€“9982) | 3089 (2002â€“3265) / 9291 (8997â€“10831) | 54.39 (51.958â€“54.493) | 307 (293â€“315) / 40 (40â€“41) |
| 512k-1000ms-3pct-1080p30 | bolt-current | 1.7 (1.3â€“1.8) | 97.5 (97.3â€“97.6) | 1835 (1611â€“1881) / 10644 (10247â€“10659) | 2669 (2552â€“7608) / 10509 (10307â€“11639) | 50.205 (49.071â€“50.715) | 289 (282â€“294) / 38 (38â€“39) |
| 512k-1000ms-3pct-1080p30 | native | 2.8 (2.7â€“2.8) | 97.1 (96.8â€“97.3) | 525 (523â€“527) / 612 (580â€“614) | 932 (901â€“1147) / 1847 (1829â€“2592) | 33.465 (33.119â€“36.075) | 372 (367â€“389) / 12 (12â€“12) |
| 512k-1000ms-3pct-360p30 | bolt-baseline | 25.3 (24.4â€“25.4) | 97 (97â€“97.5) | 566 (557â€“575) / 1056 (1032â€“1079) | 1146 (1052â€“1179) / 2174 (2095â€“2401) | 31.094 (31.046â€“32.104) | 496 (494â€“500) / 92 (90â€“92) |
| 512k-1000ms-3pct-360p30 | bolt-current | 25.3 (23.6â€“25.4) | 97.4 (96.9â€“97.6) | 588 (578â€“607) / 1047 (948â€“1082) | 1025 (1007â€“1140) / 2220 (2184â€“2302) | 26.418 (23.301â€“30.312) | 498 (497â€“498) / 92 (92â€“93) |
| 512k-1000ms-3pct-360p30 | native | 13 (11.8â€“13) | 97.2 (96.9â€“97.2) | 522 (521â€“523) / 694 (610â€“756) | 778 (762â€“868) / 1772 (1728â€“2496) | 24.76 (24.492â€“26.901) | 394 (390â€“403) / 12 (12â€“13) |
| 512k-1000ms-3pct-720p30 | bolt-baseline | 9.2 (7.5â€“9.8) | 97.1 (96.5â€“97.2) | 741 (730â€“811) / 3163 (2978â€“3461) | 1530 (1511â€“1683) / 4385 (4146â€“4524) | 43.714 (39.84â€“44.841) | 396 (383â€“405) / 51 (50â€“52) |
| 512k-1000ms-3pct-720p30 | bolt-current | 6.6 (6.2â€“6.7) | 96.3 (96.1â€“96.5) | 712 (616â€“730) / 3342 (3325â€“3498) | 1512 (1370â€“1589) / 4171 (3405â€“4830) | 44.424 (43.905â€“45.337) | 365 (355â€“379) / 48 (47â€“52) |
| 512k-1000ms-3pct-720p30 | native | 6.2 (5.6â€“7.1) | 97.1 (96.4â€“97.3) | 523 (522â€“524) / 621 (594â€“629) | 785 (736â€“815) / 1716 (1703â€“3415) | 24.402 (21.391â€“26.18) | 396 (367â€“399) / 12 (12â€“12) |
| 512k-500ms-1pct-1080p30 | bolt-baseline | 2.9 (2.2â€“2.9) | 97.9 (91.4â€“99.2) | 900 (630â€“8005) / 6927 (6914â€“9470) | 1788 (1378â€“7809) / 8084 (7700â€“10103) | 45.694 (42.335â€“46.908) | 284 (254â€“545) / 34 (31â€“51) |
| 512k-500ms-1pct-1080p30 | bolt-current | 2.8 (1.9â€“3.3) | 98.8 (98.7â€“98.8) | 594 (449â€“791) / 7022 (6591â€“8671) | 1549 (1163â€“1794) / 7165 (6530â€“8761) | 42.963 (41.208â€“50.469) | 247 (240â€“254) / 29 (26â€“29) |
| 512k-500ms-1pct-1080p30 | native | 3.2 (2.9â€“3.3) | 99.2 (98.9â€“99.3) | 268 (267â€“268) / 351 (342â€“410) | 511 (506â€“516) / 1039 (1022â€“1061) | 18.723 (18.559â€“22.767) | 371 (363â€“375) / 12 (12â€“12) |
| 512k-500ms-1pct-360p30 | bolt-baseline | 27.9 (27.8â€“28.2) | 99 (98.9â€“99.1) | 318 (316â€“319) / 707 (697â€“725) | 365 (361â€“370) / 928 (922â€“1277) | 7.488 (5.941â€“9.972) | 491 (490â€“493) / 63 (62â€“66) |
| 512k-500ms-1pct-360p30 | bolt-current | 28 (27.2â€“28.4) | 99.1 (98.9â€“99.2) | 321 (319â€“332) / 748 (704â€“749) | 360 (356â€“381) / 1310 (1125â€“1511) | 9.115 (7.025â€“10.299) | 495 (493â€“496) / 63 (62â€“65) |
| 512k-500ms-1pct-360p30 | native | 19.7 (18.5â€“19.8) | 98.9 (98.7â€“99.2) | 267 (263â€“268) / 346 (314â€“377) | 492 (487â€“498) / 828 (822â€“835) | 2.515 (2.486â€“2.568) | 399 (364â€“401) / 12 (12â€“12) |
| 512k-500ms-1pct-720p30 | bolt-baseline | 10.1 (9.6â€“11.5) | 98.9 (98.5â€“99.1) | 370 (354â€“489) / 2748 (1869â€“2752) | 774 (743â€“792) / 2990 (2813â€“3281) | 29.01 (23.399â€“30.872) | 392 (384â€“396) / 37 (35â€“40) |
| 512k-500ms-1pct-720p30 | bolt-current | 9.6 (9.3â€“10.9) | 99.2 (99.1â€“99.4) | 414 (347â€“575) / 2741 (2709â€“2746) | 786 (783â€“927) / 2808 (2755â€“4045) | 27.471 (26.229â€“27.762) | 383 (379â€“395) / 36 (36â€“36) |
| 512k-500ms-1pct-720p30 | native | 10.1 (9.4â€“10.4) | 98.8 (98.7â€“98.8) | 269 (268â€“271) / 355 (328â€“365) | 509 (489â€“520) / 876 (825â€“930) | 3.59 (2.878â€“6.84) | 392 (386â€“403) / 12 (12â€“12) |
| clean-20mbit-1080p30 | bolt-baseline | 30 (30â€“30) | 100 (100â€“100) | 15 (15â€“16) / 29 (28â€“30) | 50 (50â€“53) / 86 (84â€“97) | 0 (0â€“0) | 4682 (4680â€“4684) / 207 (206â€“207) |
| clean-20mbit-1080p30 | bolt-current | 30 (30â€“30) | 100 (100â€“100) | 16 (15â€“16) / 30 (28â€“30) | 51 (49â€“54) / 91 (91â€“95) | 0 (0â€“0) | 4674 (4670â€“4684) / 205 (205â€“207) |
| clean-20mbit-1080p30 | native | 30 (30â€“30) | 100 (100â€“100) | 11 (11â€“12) / 23 (23â€“24) | 96 (81â€“98) / 109 (92â€“110) | 0 (0â€“0) | 4317 (4317â€“4321) / 15 (15â€“15) |
| clean-20mbit-360p30 | bolt-baseline | 30 (30â€“30) | 100 (100â€“100) | 11 (11â€“11) / 14 (14â€“14) | 19 (18â€“19) / 23 (23â€“23) | 0 (0â€“0) | 1840 (1840â€“1842) / 95 (95â€“95) |
| clean-20mbit-360p30 | bolt-current | 30 (30â€“30) | 100 (100â€“100) | 11 (11â€“11) / 14 (14â€“14) | 18 (18â€“18) / 23 (23â€“23) | 0 (0â€“0) | 1840 (1839â€“1841) / 95 (95â€“95) |
| clean-20mbit-360p30 | native | 30 (29.9â€“30) | 100 (100â€“100) | 11 (11â€“11) / 13 (13â€“13) | 58 (58â€“67) / 69 (68â€“75) | 0 (0â€“0) | 1673 (1672â€“1673) / 13 (13â€“13) |
| clean-20mbit-720p30 | bolt-baseline | 30 (30â€“30) | 100 (100â€“100) | 12 (11â€“12) / 18 (18â€“19) | 27 (26â€“27) / 38 (37â€“41) | 0 (0â€“0) | 2967 (2966â€“2969) / 140 (139â€“140) |
| clean-20mbit-720p30 | bolt-current | 30 (30â€“30) | 100 (100â€“100) | 11 (11â€“12) / 18 (18â€“19) | 27 (27â€“27) / 38 (37â€“39) | 0 (0â€“0) | 2964 (2961â€“2968) / 139 (139â€“139) |
| clean-20mbit-720p30 | native | 30 (29.9â€“30) | 100 (100â€“100) | 11 (11â€“11) / 15 (15â€“16) | 50 (49â€“64) / 71 (59â€“75) | 0 (0â€“0) | 2750 (2735â€“2754) / 14 (14â€“14) |

Clean paths deliver 30 FPS and 100% audio at all profiles (native has one 29.9 FPS repeat at 360p and one at 720p). Current and baseline clean Bolt measurements are similar; this run does not establish an efficiency gain. At clean 1080p30, Bolt uses roughly 4.67 Mbit/s sender wire and 205â€“207 kbit/s reverse traffic, compared with native 4.32 Mbit/s and 15 kbit/s.

Constrained higher-resolution modeled results remain poor. Severe 720p30 current Bolt renders 6.2â€“6.7 FPS versus baseline 7.5â€“9.8, with audio delivery 96.1â€“96.5% versus 96.5â€“97.2%. Severe 1080p30 renders 1.3â€“1.8 FPS on both Bolt snapshots, and current audio p99 is 10.25â€“10.66 seconds versus baseline 8.33â€“9.98 seconds. Native also loses audio, but has far shorter audio tails. Some fixed-threshold freeze totals improve and others worsen; three independent loss runs do not establish a general improvement. These are unresolved findings, not evidence that the production audio regression is cured.

## Production transport with synthetic codecs

All 81 modeled runs and 12 production calls completed. Both source snapshots use their own AOT WASM media service, pacer, SFrame, recovery and real relay. Payloads/decoder are synthetic, so these results cannot estimate real encoder/decoder CPU or phone heat.

**Window scope:** video sent/rendered FPS, longest frame gap and reset deltas use the measured 60 seconds. Audio delivery/delay and receive recovery counters cover the lifetime, including startup and warmup. Audio delivery spans received sequence numbers and omits unseen leading/trailing loss. It cannot be compared directly with the modeled sender cohort.

**Network scope:** severe injects 3% loss on A downlink (512 kbit/s, 500 ms one-way delay), while A uplink gets 500 ms delay without injected loss. It cannot directly verify sender-uplink-loss repair fixes. WebSocket bypasses the shaped TURN path. Quality assertions, transport validity and setup success are separate outcomes.

### Baseline observations

Clean baseline: 29.9â€“30 FPS, lifetime audio 100% in both directions, longest measured frame gap 144â€“199 ms, no decoder resets or quality failures.

Severe baseline: all three calls initially opened UDP at the 5.8-second sample. A first appears on WebSocket at 18.9â€“19.0 seconds (stalled), briefly retries UDP, and is on WebSocket (flapping) by the final warmup sample at 54.1â€“54.2 seconds and every measured sample through 114.3â€“114.5 seconds. B stays UDP/relay. This is a **fallback finding; neither direction is a valid fully shaped-path performance result**. High later throughput and low later delay reflect the unshaped socket path.

All three severe baseline repeats retain the existing A->B lifetime audio quality failure: 93.63â€“95.62% delivered against the 98% assertion. B->A delivered 98.28â€“99.42%. The lifetime audio loss is not a settled-window delivery measurement.

The [deduplicated timeline](baseline-severe-timeline.json) preserves sampled paths/rate/recovery without the large recent-picture strings. Between the last warmup sample and final sample, receiver A NACK/recovered/abandoned/incomplete counters have zero growth in every repeat. Receiver B has zero NACK/recovered/abandoned growth, and incomplete changes by 0/1/1. The displayed lifetime recovery counts therefore mostly describe startup/warmup; the sample before the actual reset is approximate, not an exact window counter boundary.

The browser test waits up to 45 seconds for UDP and continues even after timeout. A final UDP path alone is insufficient to prove the whole measured window stayed UDP. Reports now expose final paths and flag fallback/unknown as invalid shaped-path evidence.

### Completed current production comparison

The [full per-repeat production table](production-report.md) retains final paths, quality failures and metric scopes. The workflow conclusion is success because completed measurement JSON is preserved even when the production quality assertions fail. This confirms runnable setup, not quality success. Quality failures also remain in each severe artifact folder.

| scenario / snapshot | direction | measured rendered FPS range | measured longest gap ms range | reset delta range | lifetime audio delivery % range | lifetime audio p50 / p99 ms ranges | path validity |
|---|---|---|---|---|---|---|---|
| clean-1080p30 / baseline | A->B | 29.9-30 | 157-199 | 0-0 | 100-100 | 35-35 / 58-74 | sampled UDP, both peers |
| clean-1080p30 / baseline | B->A | 30-30 | 144-180 | 0-0 | 100-100 | 34-35 / 72-88 | sampled UDP, both peers |
| clean-1080p30 / current | A->B | 30-30 | 149-182 | 0-0 | 100-100 | 33-35 / 56-68 | sampled UDP, both peers |
| clean-1080p30 / current | B->A | 30-30 | 148-189 | 0-0 | 100-100 | 34-35 / 72-79 | sampled UDP, both peers |
| 512k-1000ms-3pct / baseline | A->B | 23.6-30 | 110-141 | 0-0 | 93.63-95.62 | 19-20 / 182-297 | invalid complete shaped window: A fallback |
| 512k-1000ms-3pct / baseline | B->A | 20-30 | 116-151 | 0-0 | 98.28-99.42 | 20-22 / 103-1064 | invalid complete shaped window: A fallback |
| 512k-1000ms-3pct / current | A->B | 1.5-30 | 87-32536 | 0-0 | 91.47-95.66 | 18-21 / 196-1512 | invalid complete shaped window: A fallback |
| 512k-1000ms-3pct / current | B->A | 7.5-25 | 122-25025 | 0-3 | 98.02-99.52 | 18-21 / 47-2322 | invalid complete shaped window: A fallback |

Current clean calls passed all existing quality assertions: 30 FPS, 100% lifetime audio delivery, no resets and 148-189 ms measured frame gaps. Baseline clean also passed. Sampled clean open/warmup/measure paths remain UDP for both peers; sampling cannot rule out transitions between samples.

All current severe repeats initially opened UDP at 5.6-5.7 seconds; A first appears on stalled WebSocket at 18.8 seconds. Repeats 2/3 settle on flapping WebSocket before measurement. Repeat 1 retries UDP during measurement (samples at 58.8-98.9 seconds), then appears on flapping WebSocket at 103.9 seconds. Its sender A remains video-suspended in most of those UDP samples (48 kbit/s total, zero video budget), briefly resumes 180p12, and suspends again. It has measured 32.536-second A->B and 25.025-second B->A frame gaps, plus three B->A decoder resets. Those are measured-window quality failures, not merely startup audio loss. The final WebSocket path and mixed transport window prevent a valid complete-window shaped throughput comparison.

All three current severe repeats also fail existing A->B lifetime audio delivery: 91.47%, 94.74%, 95.66%. Baseline severe values are 95.62%, 93.63%, 95.60%; independent random loss and fallback timing prevent attributing differences to an individual fix. No audio cure is demonstrated.

The [current sampled severe timeline](current-severe-timeline.json) preserves rate decisions and approximate window counter boundaries. Repeat 1 receiver A changes by NACK +7, recovered +0, abandoned +3 and incomplete +10 from last warmup sample to final sample; receiver B changes by +60/+6/+15/+2. Repeats 2/3 have no growth for those counters. Compare these deltas with baseline's mostly unchanged measurement counters rather than treating lifetime totals as measured-window improvements.

Neither experiment measures audible playout delay, real hardware codec CPU, battery drain or device temperature. There is no evidence here for a heating improvement. The constrained model uses a requested 300 kbit/s video ceiling at every resolution to expose compression/recovery behavior; it does not impose a new product resolution cap.

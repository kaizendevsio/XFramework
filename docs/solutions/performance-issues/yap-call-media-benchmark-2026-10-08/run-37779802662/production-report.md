
Production media path with synthetic codecs

| run | direction | final sender / receiver path | shaped-path validity | sent fps / kbps | rendered fps | longest frame gap ms | decoder resets | lifetime audio delivery % (sequence span) | lifetime audio delay p50 / p99 ms | settled picture | quality failures |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 512k-1000ms-3pct.baseline.r1 | A->B | WebSocket / UDP/relay | invalid shaped path: fallback or unknown | 29.8 / 4939 | 29.8 | 139 | 0 | 95.62 | 19 / 224 | 1080p30 | ['A->B: audio delivered 0.9562081401339516'] |
| 512k-1000ms-3pct.baseline.r1 | B->A | UDP/relay / WebSocket | invalid shaped path: fallback or unknown | 30 / 5731 | 30 | 138 | 0 | 99.42 | 21 / 103 | 1080p30 | ['A->B: audio delivered 0.9562081401339516'] |
| 512k-1000ms-3pct.baseline.r2 | A->B | WebSocket / UDP/relay | invalid shaped path: fallback or unknown | 23.6 / 2061 | 23.6 | 141 | 0 | 93.63 | 20 / 297 | 1080p30 | ['A->B: audio delivered 0.936291522062469'] |
| 512k-1000ms-3pct.baseline.r2 | B->A | UDP/relay / WebSocket | invalid shaped path: fallback or unknown | 20 / 726 | 20 | 116 | 0 | 98.76 | 20 / 276 | 360p20 | ['A->B: audio delivered 0.936291522062469'] |
| 512k-1000ms-3pct.baseline.r3 | A->B | WebSocket / UDP/relay | invalid shaped path: fallback or unknown | 30 / 5232 | 30 | 110 | 0 | 95.60 | 19 / 182 | 1080p30 | ['A->B: audio delivered 0.9559910606842015'] |
| 512k-1000ms-3pct.baseline.r3 | B->A | UDP/relay / WebSocket | invalid shaped path: fallback or unknown | 20 / 710 | 20 | 151 | 0 | 98.28 | 22 / 1064 | 360p20 | ['A->B: audio delivered 0.9559910606842015'] |
| 512k-1000ms-3pct.current.r1 | A->B | WebSocket / UDP/relay | invalid shaped path: fallback or unknown | 2.3 / 27 | 1.5 | 32536 | 0 | 91.47 | 21 / 1512 | 240p15 | ['A->B: rendered 1.5 fps of 2.3 sent', 'A->B: froze for 32536 ms', 'A->B: audio delivered 0.9146725440806045', 'B->A: rendered 7.5 fps of 10.0 sent', 'B->A: froze for 25025 ms', 'B->A: decoder restarted 3 times'] |
| 512k-1000ms-3pct.current.r1 | B->A | UDP/relay / WebSocket | invalid shaped path: fallback or unknown | 10 / 277 | 7.5 | 25025 | 3 | 99.52 | 18 / 2322 | 360p20 | ['A->B: rendered 1.5 fps of 2.3 sent', 'A->B: froze for 32536 ms', 'A->B: audio delivered 0.9146725440806045', 'B->A: rendered 7.5 fps of 10.0 sent', 'B->A: froze for 25025 ms', 'B->A: decoder restarted 3 times'] |
| 512k-1000ms-3pct.current.r2 | A->B | WebSocket / UDP/relay | invalid shaped path: fallback or unknown | 17.4 / 646 | 17.4 | 157 | 0 | 94.74 | 18 / 271 | 900p30 | ['A->B: audio delivered 0.9473684210526315'] |
| 512k-1000ms-3pct.current.r2 | B->A | UDP/relay / WebSocket | invalid shaped path: fallback or unknown | 25 / 1310 | 25 | 122 | 0 | 98.02 | 18 / 47 | 540p25 | ['A->B: audio delivered 0.9473684210526315'] |
| 512k-1000ms-3pct.current.r3 | A->B | WebSocket / UDP/relay | invalid shaped path: fallback or unknown | 30 / 5712 | 30 | 87 | 0 | 95.66 | 18 / 196 | 1080p30 | ['A->B: audio delivered 0.9565816678152997'] |
| 512k-1000ms-3pct.current.r3 | B->A | UDP/relay / WebSocket | invalid shaped path: fallback or unknown | 25 / 1269 | 25 | 126 | 0 | 98.04 | 21 / 118 | 540p25 | ['A->B: audio delivered 0.9565816678152997'] |
| clean-1080p30.baseline.r1 | A->B | UDP/relay / UDP/relay | final UDP; inspect timeline | 30 / 5722 | 30 | 158 | 0 | 100.00 | 35 / 74 | 1080p30 | [] |
| clean-1080p30.baseline.r1 | B->A | UDP/relay / UDP/relay | final UDP; inspect timeline | 30 / 5720 | 30 | 144 | 0 | 100.00 | 35 / 72 | 1080p30 | [] |
| clean-1080p30.baseline.r2 | A->B | UDP/relay / UDP/relay | final UDP; inspect timeline | 30 / 5715 | 30 | 157 | 0 | 100.00 | 35 / 65 | 1080p30 | [] |
| clean-1080p30.baseline.r2 | B->A | UDP/relay / UDP/relay | final UDP; inspect timeline | 30 / 5709 | 30 | 149 | 0 | 100.00 | 34 / 75 | 1080p30 | [] |
| clean-1080p30.baseline.r3 | A->B | UDP/relay / UDP/relay | final UDP; inspect timeline | 30 / 5718 | 29.9 | 199 | 0 | 100.00 | 35 / 58 | 1080p30 | [] |
| clean-1080p30.baseline.r3 | B->A | UDP/relay / UDP/relay | final UDP; inspect timeline | 30 / 5740 | 30 | 180 | 0 | 100.00 | 35 / 88 | 1080p30 | [] |
| clean-1080p30.current.r1 | A->B | UDP/relay / UDP/relay | final UDP; inspect timeline | 30 / 5676 | 30 | 149 | 0 | 100.00 | 33 / 66 | 1080p30 | [] |
| clean-1080p30.current.r1 | B->A | UDP/relay / UDP/relay | final UDP; inspect timeline | 30 / 5734 | 30 | 149 | 0 | 100.00 | 34 / 72 | 1080p30 | [] |
| clean-1080p30.current.r2 | A->B | UDP/relay / UDP/relay | final UDP; inspect timeline | 30 / 5747 | 30 | 152 | 0 | 100.00 | 34 / 68 | 1080p30 | [] |
| clean-1080p30.current.r2 | B->A | UDP/relay / UDP/relay | final UDP; inspect timeline | 30 / 5736 | 30 | 148 | 0 | 100.00 | 35 / 73 | 1080p30 | [] |
| clean-1080p30.current.r3 | A->B | UDP/relay / UDP/relay | final UDP; inspect timeline | 30 / 5725 | 30 | 182 | 0 | 100.00 | 35 / 56 | 1080p30 | [] |
| clean-1080p30.current.r3 | B->A | UDP/relay / UDP/relay | final UDP; inspect timeline | 30 / 5728 | 30 | 189 | 0 | 100.00 | 34 / 79 | 1080p30 | [] |

Both production snapshots use their real media service, pacer, recovery, SFrame and relay. Codec payloads and decoder are synthetic; this measures transport behavior, not real decoder CPU or thermal load. Both camera preferences are 1080p30; production rate control may adapt. Video FPS and longest frame gap cover the measured window; audio and receive recovery counters cover the call lifetime, including startup/warmup. Audio metrics span received sequence numbers and lifetime delay samples; they exclude unobserved leading/trailing loss and are not directly comparable to the modeled cohort metric. WebSocket fallback bypasses TURN/netem and invalidates shaped-path performance claims even if the quality assertions pass. The test waits up to 45 seconds for UDP but continues after timeout; final UDP alone does not establish continuous UDP throughout measurement. Severe shaping injects loss on A downlink; A uplink has delay without injected loss, so this does not directly verify sender-uplink loss repairs. Revisions are included in the uploaded artifacts.


Production media path with synthetic codecs

| run | direction | final sender / receiver path | shaped-path validity | sent fps / kbps | rendered fps | longest frame gap ms | decoder resets | lifetime audio delivery % (sequence span) | lifetime audio delay p50 / p99 ms | settled picture | quality failures |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 512k-1000ms-3pct.baseline.r1 | A->B | WebSocket / UDP/relay | invalid shaped path: fallback or unknown | 26.2 / 2839 | 26.2 | 156 | 0 | 94.52 | 20 / 266 | 1080p30 | ['A->B: audio delivered 0.9451759119361105'] |
| 512k-1000ms-3pct.baseline.r1 | B->A | UDP/relay / WebSocket | invalid shaped path: fallback or unknown | 25 / 1267 | 25 | 109 | 0 | 98.98 | 19 / 2060 | 540p25 | ['A->B: audio delivered 0.9451759119361105'] |
| 512k-1000ms-3pct.baseline.r2 | A->B | WebSocket / UDP/relay | invalid shaped path: fallback or unknown | 19.2 / 1278 | 18.5 | 7587 | 1 | 94.03 | 22 / 449 | 1080p30 | ['A->B: froze for 7587 ms', 'A->B: audio delivered 0.9402735138152386'] |
| 512k-1000ms-3pct.baseline.r2 | B->A | UDP/relay / WebSocket | invalid shaped path: fallback or unknown | 21.8 / 1063 | 21.8 | 142 | 0 | 99.09 | 19 / 2323 | 900p30 | ['A->B: froze for 7587 ms', 'A->B: audio delivered 0.9402735138152386'] |
| 512k-1000ms-3pct.baseline.r3 | A->B | WebSocket / UDP/relay | invalid shaped path: fallback or unknown | 17.4 / 676 | 17.4 | 163 | 0 | 94.34 | 21 / 405 | 900p30 | ['A->B: audio delivered 0.9433614330874605'] |
| 512k-1000ms-3pct.baseline.r3 | B->A | UDP/relay / WebSocket | invalid shaped path: fallback or unknown | 30 / 5710 | 30 | 122 | 0 | 99.71 | 19 / 2067 | 1080p30 | ['A->B: audio delivered 0.9433614330874605'] |
| 512k-1000ms-3pct.current.r1 | A->B | WebSocket / UDP/relay | invalid shaped path: fallback or unknown | 30 / 4057 | 30 | 130 | 0 | 95.56 | 19 / 195 | 1080p30 | ['A->B: audio delivered 0.9556090846524432'] |
| 512k-1000ms-3pct.current.r1 | B->A | UDP/relay / WebSocket | invalid shaped path: fallback or unknown | 25 / 1290 | 25 | 99 | 0 | 98.19 | 21 / 1850 | 540p25 | ['A->B: audio delivered 0.9556090846524432'] |
| 512k-1000ms-3pct.current.r2 | A->B | WebSocket / UDP/relay | invalid shaped path: fallback or unknown | 28.7 / 3838 | 28.8 | 124 | 0 | 95.56 | 21 / 178 | 1080p30 | ['A->B: audio delivered 0.95562435500516'] |
| 512k-1000ms-3pct.current.r2 | B->A | UDP/relay / WebSocket | invalid shaped path: fallback or unknown | 25 / 1293 | 25 | 122 | 0 | 98.14 | 19 / 1995 | 540p25 | ['A->B: audio delivered 0.95562435500516'] |
| 512k-1000ms-3pct.current.r3 | A->B | WebSocket / UDP/relay | invalid shaped path: fallback or unknown | 21 / 1371 | 21 | 156 | 0 | 93.90 | 19 / 298 | 1080p30 | ['A->B: audio delivered 0.9390098062664435'] |
| 512k-1000ms-3pct.current.r3 | B->A | UDP/relay / WebSocket | invalid shaped path: fallback or unknown | 20 / 684 | 20 | 138 | 0 | 98.35 | 20 / 2064 | 360p20 | ['A->B: audio delivered 0.9390098062664435'] |
| clean-1080p30.baseline.r1 | A->B | UDP/relay / UDP/relay | final UDP; inspect timeline | 30 / 5699 | 30 | 178 | 0 | 100.00 | 35 / 90 | 1080p30 | [] |
| clean-1080p30.baseline.r1 | B->A | UDP/relay / UDP/relay | final UDP; inspect timeline | 30 / 5700 | 29.9 | 213 | 0 | 100.00 | 36 / 74 | 1080p30 | [] |
| clean-1080p30.baseline.r2 | A->B | UDP/relay / UDP/relay | final UDP; inspect timeline | 30 / 5727 | 30 | 163 | 0 | 100.00 | 36 / 66 | 1080p30 | [] |
| clean-1080p30.baseline.r2 | B->A | UDP/relay / UDP/relay | final UDP; inspect timeline | 30 / 5712 | 30 | 166 | 0 | 100.00 | 37 / 66 | 1080p30 | [] |
| clean-1080p30.baseline.r3 | A->B | UDP/relay / UDP/relay | final UDP; inspect timeline | 30 / 5712 | 30 | 196 | 0 | 100.00 | 35 / 62 | 1080p30 | [] |
| clean-1080p30.baseline.r3 | B->A | UDP/relay / UDP/relay | final UDP; inspect timeline | 30 / 5729 | 30 | 177 | 0 | 100.00 | 35 / 81 | 1080p30 | [] |
| clean-1080p30.current.r1 | A->B | UDP/relay / UDP/relay | final UDP; inspect timeline | 30 / 5747 | 30 | 158 | 0 | 100.00 | 33 / 60 | 1080p30 | [] |
| clean-1080p30.current.r1 | B->A | UDP/relay / UDP/relay | final UDP; inspect timeline | 30 / 5734 | 30 | 163 | 0 | 100.00 | 35 / 73 | 1080p30 | [] |
| clean-1080p30.current.r2 | A->B | UDP/relay / UDP/relay | final UDP; inspect timeline | 30 / 5723 | 30 | 153 | 0 | 100.00 | 34 / 59 | 1080p30 | [] |
| clean-1080p30.current.r2 | B->A | UDP/relay / UDP/relay | final UDP; inspect timeline | 30 / 5703 | 30 | 146 | 0 | 100.00 | 34 / 63 | 1080p30 | [] |
| clean-1080p30.current.r3 | A->B | UDP/relay / UDP/relay | final UDP; inspect timeline | 30 / 5721 | 30 | 180 | 0 | 100.00 | 34 / 61 | 1080p30 | [] |
| clean-1080p30.current.r3 | B->A | UDP/relay / UDP/relay | final UDP; inspect timeline | 30 / 5696 | 30 | 160 | 0 | 100.00 | 34 / 82 | 1080p30 | [] |

Both production snapshots use their real media service, pacer, recovery, SFrame and relay. Codec payloads and decoder are synthetic; this measures transport behavior, not real decoder CPU or thermal load. Both camera preferences are 1080p30; production rate control may adapt. Video FPS and longest frame gap cover the measured window; audio and receive recovery counters cover the call lifetime, including startup/warmup. Audio metrics span received sequence numbers and lifetime delay samples; they exclude unobserved leading/trailing loss and are not directly comparable to the modeled cohort metric. WebSocket fallback bypasses TURN/netem and invalidates shaped-path performance claims even if the quality assertions pass. The test waits up to 45 seconds for UDP but continues after timeout; final UDP alone does not establish continuous UDP throughout measurement. Severe shaping injects loss on A downlink; A uplink has delay without injected loss, so this does not directly verify sender-uplink loss repairs. Revisions are included in the uploaded artifacts.

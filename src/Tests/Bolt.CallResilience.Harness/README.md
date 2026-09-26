# Call relay network harness

An opt-in, repeatable measurement of what a degraded mobile link does to a Yap call. It runs the
real `Bolt.Server` group relay with Yap's relay options (`YapCallGateway`) and two participants:

- **sender** — inside the relay container, on loopback (an unshaped uplink). It publishes one audio
  and one video stream the way the browser client does: clear MediaFrame headers (sequence,
  timestamp, keyframe flag on a keyframe's first fragment), payloads the size of SFrame ciphertext,
  pictures cut into 4 KB fragments, and the encoder's keyframe policy.
- **receiver** — in a second container, behind `tc netem` on the relay's egress (the downlink to the
  phone) and on its own egress (the uplink that carries its TCP ACKs).

The receiver measures one-way delay (both containers share the host clock), audio continuity and
how many pictures a decoder could actually show. The relay reports whether anyone was retired or
removed, plus its drop counters. Nothing is mocked below the WebSocket: this is real Linux TCP.

## Run it in CI

`.github/workflows/call-network-harness.yml` runs every profile for the base branch (with the
previous client defaults) and for the PR branch (with the new ones), in parallel, and writes a
table to the job summary. It runs on pull requests that touch the relay, or on demand from the
Actions tab ("Call network harness", optional call length).

## Run it locally

Needs Docker on Linux (or a Linux Docker engine) with the `sch_netem` kernel module available.

```bash
dotnet publish src/Tests/Bolt.CallResilience.Harness -c Release -o out/harness
docker build -t bolt-call-harness -f src/Tests/Bolt.CallResilience.Harness/Dockerfile out/harness

# 512 kbps, 1 s RTT, 1% loss, a 240p15 + Opus 32 kbps call for three minutes
bash src/Tests/Bolt.CallResilience.Harness/run-profile.sh m512 bolt-call-harness \
  "delay 500ms 20ms rate 512kbit loss 1%" "delay 500ms 20ms" -1 0 survive \
  SECONDS=180 VIDEO_KBPS=180 FPS=15 AUDIO_PAYLOAD=104 KF_MS=10000

# a 10 s outage on a 2 Mbit 4G link, 60 s into the call
bash src/Tests/Bolt.CallResilience.Harness/run-profile.sh outage10 bolt-call-harness \
  "delay 50ms 10ms rate 2mbit" "delay 50ms 10ms" 60 10 any SECONDS=180

python3 src/Tests/Bolt.CallResilience.Harness/summarize.py logs
```

To measure another relay with the same harness, publish with
`-p:BoltServerProject=/path/to/other/checkout/src/Libraries/Bolt/Bolt.Server/Bolt.Server.csproj`.
Options that relay does not have are skipped.

## Settings (relay container environment)

| Variable | Default | Meaning |
|---|---|---|
| `SECONDS` | 180 | Call length after both participants joined |
| `VIDEO_KBPS`, `FPS` | 180, 15 | Offered video (0 disables video) |
| `KF_RATIO` | 6 | Keyframe size as a multiple of a delta picture |
| `KF_MS` | 10000 | Safety keyframe interval |
| `KF_ON_DEMAND` | 1 | Honour keyframe requests from the relay/receivers, at most one per second |
| `AUDIO_PAYLOAD` | 104 | Audio payload bytes per 20 ms (104 = Opus 32 kbps + SFrame; 347 = Opus 128 kbps) |
| `DEADLINE_MS` | 250 | `SendEnqueueTimeoutMs` (Yap's value) |
| `STALL_MS` | 15000 | `TransportSendStallTimeoutMs`, the progress watchdog (newer relays only) |
| `AUTH_DELAY_MS` | 30 | Latency of every participant authorization check |
| `AUTH_FAIL_AT_S`, `AUTH_FAIL_FOR_S` | off | Make authorization throw for a window, like a hub reconnect |
| `AUTH_GRACE_S` | 120 | `GroupAuthorizationGraceSeconds` (newer relays only) |

## Reading the result

`SUMMARY` lines are JSON. The relay's `outcome` is `survived` unless a participant was retired,
removed or disconnected (with the time). On the receiver, `audioDelayMs` is the one-way delay
distribution of every audio packet, `audioDelivered` the share of audio sequence numbers that
arrived, `picturesDecodable` the pictures a decoder could show (a keyframe, or the picture after
one it showed), and `frozenSeconds` the seconds after the first picture with none.

What it does not model: cellular link-layer retransmission, an IP address change (a certain TCP
death that needs call resumption), the extra buffering of the production ingress path, and
sender-side adaptation (the offered load is constant).

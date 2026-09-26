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

## Resumable calls (RESUME=1)

With `RESUME=1` the relay container also plays the Yap gateway's resume contract (`ResumeHost.cs`):
single-use tickets bound to the seat generation they replace, a seat held for `GRACE_S` (45) after
its socket ends, a resume that supersedes a socket the server still thinks is alive, WebSocket
keep-alive pings with a 20 s timeout, and the call ending only when a hold runs out. The relay is
still the real `BoltServer`; the gateway's own implementation of these rules is tested in Yap.Tests.

The receiver (`ResumingReceiver.cs`) then behaves like the phone: a heartbeat every 2 s, the app's
own `CallLinkMonitor` to decide the link is dead (RTT-scaled), and the app's own `CallReconnector`
to pace the resume attempts (both files are compiled in from `Bolt.Media`). Each attempt is a new TCP
connection: a ticket, a socket, registration, then `/ready` to rejoin the relay's room.

| Variable | Default | Meaning |
|---|---|---|
| `RESUME` | 0 | 1 holds the receiver's seat and lets it resume |
| `GRACE_S` | 45 | Seat hold, and the receiver's give-up time |
| `IPCHANGE_AT_S` | off | At this second the receiver's connection is blackholed both ways (iptables) and it continues from a second address |
| `RX_STALL_AT_S`, `RX_STALL_FOR_S` | off | The receiver stops reading for a while (a frozen tab), so the relay's stall watchdog retires it |

The receiver's summary lists each resume (`timeToResumeMs` from noticing the loss to being back),
`audioBackAfterMs` / `videoBackAfterMs` (from the network returning, or the app unfreezing, to the
first live audio packet / decodable picture, one delivered within 2 s of being sent), and whether it gave up. The relay's summary says whether
the other side's call survived and how long the seat was held. `summarize.py` prints a second table
for these runs. Expectations in `run-profile.sh`: `resume`, `resume-retired` and `end-clean`.

## Reading the result

`SUMMARY` lines are JSON. The relay's `outcome` is `survived` unless a participant was retired,
removed or disconnected (with the time). On the receiver, `audioDelayMs` is the one-way delay
distribution of every audio packet, `audioDelivered` the share of audio sequence numbers that
arrived, `picturesDecodable` the pictures a decoder could show (a keyframe, or the picture after
one it showed), and `frozenSeconds` the seconds after the first picture with none.

What it does not model: cellular link-layer retransmission, the extra buffering of the production
ingress path, sender-side adaptation (the offered load is constant), and browser network hints
(`online`, network change) that let the app retry a resume the moment a network appears.

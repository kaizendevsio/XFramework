# Call relay network harness

An opt-in, repeatable measurement of what a degraded mobile link does to a Yap call. It runs the
real `Bolt.Server` group relay with Yap's relay options (`YapCallGateway`) and two participants:

- **sender** — inside the relay container, on loopback (an unshaped uplink). It publishes one audio
  and one video stream the way the browser client does: clear MediaFrame headers (sequence,
  timestamp, keyframe flag on a keyframe's first fragment, temporal layer), payloads the size of
  SFrame ciphertext, pictures cut into 4 KB fragments, and the encoder's keyframe policy.
  - By default it offers a constant load (`VIDEO_KBPS`, `FPS`).
  - With `ADAPTIVE=1` it is the browser client's real send path from `Bolt.Media`: the pacer (audio
    first, whole pictures), `SendRateController` fed by the relay's congestion reports and the
    receiver's delay reports, and `VideoRateLadder` choosing size, frame rate and bitrate. Only the
    encoder is a model: pictures at the chosen rate, keyframes `KF_RATIO` times a delta, temporal
    layers L1T3/L1T2 sized like H.264's, and `OVERSHOOT_PCT` for an encoder that misses its target.
- **receiver** — in a second container, behind `tc netem` on the relay's egress (the downlink to the
  phone) and on its own egress (the uplink that carries its TCP ACKs).

The receiver measures one-way delay (both containers share the host clock), audio continuity and
how many pictures a decoder could actually show: every picture names the picture it refers to, so a
relay or sender that dropped a reference shows up as undecodable pictures. In adaptive builds it
also sends the browser receiver's delay report back every 250 ms. The relay reports whether anyone
was retired or removed, plus its drop counters. Nothing is mocked below the WebSocket: this is real
Linux TCP.

## Run it in CI

`.github/workflows/call-network-harness.yml` runs every profile for the base branch (with a
constant sender) and for the PR branch (with the adaptive sender where the profile says so), in
parallel, and writes tables to the job summary. It runs on pull requests that touch the relay, or on
demand from the Actions tab ("Call network harness", optional call length).

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

# the adaptive sender starting from a 1080p offer on 512 kbps, 500 ms RTT, 1% loss
bash src/Tests/Bolt.CallResilience.Harness/run-profile.sh adapt bolt-call-harness \
  "delay 250ms 10ms rate 512kbit loss 1%" "delay 250ms 10ms" -1 0 survive ADAPTIVE=1 START_HEIGHT=1080

# a bandwidth step down to 512 kbps at 60 s and back up at 120 s
NETEM_STEPS="60=delay 50ms 10ms rate 512kbit|120=delay 50ms 10ms rate 4mbit" \
  bash src/Tests/Bolt.CallResilience.Harness/run-profile.sh step bolt-call-harness \
  "delay 50ms 10ms rate 4mbit" "delay 50ms 10ms" -1 0 survive ADAPTIVE=1

python3 src/Tests/Bolt.CallResilience.Harness/summarize.py logs
```

To measure another relay with the same harness, publish with
`-p:BoltServerProject=/path/to/other/checkout/src/Libraries/Bolt/Bolt.Server/Bolt.Server.csproj -p:HarnessAdaptive=false`.
Options that relay does not have are skipped. The adaptive sender needs this checkout's `Bolt.Media`
(and so its protocol), so a build against another relay uses the constant sender.

## Settings (relay container environment)

| Variable | Default | Meaning |
|---|---|---|
| `SECONDS` | 180 | Call length after both participants joined |
| `VIDEO_KBPS`, `FPS` | 180, 15 | Constant sender: offered video (0 disables video) |
| `KF_RATIO` | 6 | Keyframe size as a multiple of a delta picture |
| `KF_MS` | 10000 | Safety keyframe interval |
| `KF_ON_DEMAND` | 1 | Constant sender: honour keyframe requests from the relay/receivers, at most one per second |
| `AUDIO_PAYLOAD` | 104 | Constant sender: audio payload bytes per 20 ms (104 = Opus 32 kbps + SFrame; 347 = Opus 128 kbps) |
| `ADAPTIVE` | 0 | 1 runs the adaptive sender (the browser's send path) instead of the constant one |
| `START_HEIGHT` | 240 | Adaptive: the picture to start from; 720 or 1080 is an overload offer on a mobile link |
| `AUDIO_KBPS` | 32 | Adaptive: Opus rate before the rate loop moves it |
| `SVC` | 1 | Adaptive: 0 encodes without temporal layers |
| `OVERSHOOT_PCT` | 100 | Adaptive: encoder output as a percentage of its target |
| `DEADLINE_MS` | 250 | `SendEnqueueTimeoutMs` (Yap's value) |
| `STALL_MS` | 15000 | `TransportSendStallTimeoutMs`, the progress watchdog (newer relays only) |
| `AUTH_DELAY_MS` | 30 | Latency of every participant authorization check |
| `AUTH_FAIL_AT_S`, `AUTH_FAIL_FOR_S` | off | Make authorization throw for a window, like a hub reconnect |
| `AUTH_GRACE_S` | 120 | `GroupAuthorizationGraceSeconds` (newer relays only) |

`NETEM_STEPS` (in the environment of `run-profile.sh`, not the container) changes the downlink
during the call: `"AT=NETEM|AT=NETEM"`, with AT in seconds after both participants joined.

## Reading the result

`SUMMARY` lines are JSON. The relay's `outcome` is `survived` unless a participant was retired,
removed or disconnected (with the time). On the receiver, `audioDelayMs` is the one-way delay
distribution of every audio packet, `audioDelivered` the share of audio sequence numbers that
arrived, `picturesDecodable` the pictures a decoder could show (a keyframe, or a picture whose
reference it showed; `decodableByLayer` splits them by temporal layer), and `frozenSeconds` the
seconds after the first picture with none.

Adaptive runs add a `rate` object to the relay's summary: the settled video bitrate and estimate,
the final picture, when the video bitrate settled, how often the picture size changed (in total and
after the first 30 s), how long video was suspended, and the picture timeline. `RATE` lines log the
controller once a second.

What it does not model: cellular link-layer retransmission, an IP address change (a certain TCP
death that needs call resumption), the extra buffering of the production ingress path, a real
encoder's rate control, and a congested sender uplink (the sender is on loopback, so the relay's
reports and the receiver's delay reports drive the adaptation; the pacer's uplink gate is covered
by unit tests). netem's default queue (1000 packets) is deep: a link that shrinks while it is full,
as in the step-down profile, holds seconds of data no sender can take back.

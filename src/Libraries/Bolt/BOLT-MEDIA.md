# Bolt Media Streaming — Voice/Video Calls over Bolt Protocol

## Overview

Bolt Media is an experimental extension of the Bolt binary RPC protocol for audio/video call research. The repository contains protocol and processing primitives, but the audited end-to-end media path is not production-ready and is not a replacement for WebRTC.

For standard XFramework module RPC, prefer the generated `[BoltHandler]` plus `IBoltRequest<TRequest, TResponse>` pattern documented in `BOLT.md`. Bolt Media is the specialized media-streaming layer, not the default pattern for CRUD or feature-command handlers.

**Current status:** the general Hub media, ECDH and group-conference paths remain quarantined. A separate, explicitly scoped implementation now serves Yap through its authenticated HTTPS host. See [Yap voice trusted-server relay](../../../docs/solutions/architecture-patterns/yap-voice-trusted-server-relay.md) for its security decision, browser verification and device limitations. This transport-encrypted relay is not end-to-end encrypted and is not a replacement for WebRTC.

Yap's encrypted group path additionally carries camera video under the same per-epoch SFrame key as the audio. Because the SFrame session accepts at most 4096 plaintext bytes per operation, an encoded picture is cut into fragments that each go through the same authenticated encryption as an Opus packet; the fragment header (picture ID, index, count, keyframe flag, timestamp, and the picture's orientation) travels inside that plaintext, so the relay cannot see or forge a picture boundary or turn a picture. The relay routes `MediaType.Video` only for `AV1`, `VP9` and `H264`, and only with the encrypted-payload flag set. Nothing outside that path is unquarantined.

### Deployment Containment

Bolt Hub enforces `BoltConfiguration:MediaEnabled`, which defaults to `false` and is explicitly disabled in every XFramework Hub environment and Compose deployment. Deployments must not override this shared Hub quarantine or route production media clients to it. The documented Yap exception uses a dedicated media-only server with `AuthenticatedMediaOnly`, an authorization policy, server-assigned identities and explicit `TrustedServerTls` configuration. Only that authenticated WSS voice path may instantiate the browser media services for Yap; other media capabilities remain unavailable. Recognition of media frame types alone does not constitute production enablement.

QUIC/WebTransport and direct P2P are not wired as supported end-to-end transports. They must remain absent from negotiated capabilities and production documentation until secure browser and server integration tests pass.

---

## Architecture

```
Caller                    Bolt Hub (SFU)                    Callee
  |                           |                               |
  |-- CallSignal(Initiate) -->|-- CallSignal(Initiate) ------>|
  |<-- CallSignal(Ring) ------|                               |
  |                           |<-- CallSignal(Answer) --------|
  |<-- CallSignal(Answer) ----|                               |
  |                           |                               |
  |-- MediaConfig (audio) --->|-- MediaConfig (audio) ------->|
  |-- MediaConfig (video) --->|-- MediaConfig (video) ------->|
  |                           |                               |
  |== MediaFrame (audio) ====>|== MediaFrame (audio) ========>|
  |== MediaFrame (video) ====>|== MediaFrame (video) ========>|
  |<== MediaFrame (audio) ====|<== MediaFrame (audio) ========|
  |<== MediaFrame (video) ====|<== MediaFrame (video) ========|
  |                           |                               |
  |                      Media Tap                            |
  |                    (non-blocking copy)                    |
  |                           |                               |
  |                    IMediaProcessor                        |
  |                  (recording, transcription, AI)           |
```

### Group Calls (SFU Mode)

```
  Participant A ──MediaFrame──> Bolt Hub ──MediaFrame──> Participant B
  Participant B ──MediaFrame──> Bolt Hub ──MediaFrame──> Participant A
  Participant C ──MediaFrame──> Bolt Hub ──MediaFrame──> Participant A
                                         ──MediaFrame──> Participant B
```

The intended SFU path forwards encoded payloads without codec decoding. The current implementation still performs copies and has not passed the required bounded-memory fanout tests.

---

## Wire Protocol

### Frame Types

| Type | Byte | Header Size | Purpose |
|------|------|-------------|---------|
| MediaConfig | 0x20 | 52 bytes + extension | Codec/resolution negotiation |
| MediaFrame | 0x21 | 30 bytes + payload | Encoded audio/video frame |
| MediaFeedback | 0x22 | 32 bytes, 40 with a delay report, 44 with a downlink | Receiver reports loss, jitter, RTT; optionally its end-to-end queuing delay and received rate, and its own measured downlink |
| MediaKeyRequest | 0x23 | 17 bytes (fixed) | Request keyframe from sender |
| CallSignal | 0x24 | 22 bytes + payload | Call lifecycle signaling |
| FecFrame | 0x25 | 26 bytes + payload | XOR parity for error correction |
| NackRequest | 0x26 | 19 bytes + 4 per sequence | Retransmission request (never sent over a WebSocket) |
| MediaCongestion | 0x27 | 32 bytes (fixed) | Relay-to-sender congestion report; only a relay originates it |
| MediaTransport | 0x28 | 7 bytes + JSON | Datagram path signalling between one participant and the relay (WebSocket only) |
| MediaBundle | 0x29 | 2 bytes + frames | Up to four whole MediaFrames in one datagram (audio redundancy; datagram path only) |
| Padding | 0x2D | 8 bytes + zeros | Start probe filler; the relay drops it or echoes it to its sender (datagram path only) |

### MediaFrame Header (30 bytes)

```
[1:type=0x21] [16:streamId] [4:sequenceNumber] [4:timestamp] [1:flags] [4:payloadLen] [payload]
```

- **sequenceNumber** — monotonic per-stream, for ordering + gap detection
- **timestamp** — RTP-style media clock (48kHz for audio, 90kHz for video)
- **flags** — bit 0: keyframe (first fragment of a keyframe), bits 1-2: temporal layer of a video picture (0 = base), bit 3: FEC-protected, bit 4: encrypted, bit 6: drop-eligible, bit 7: compressed. Bit 2 means "silence indicator" on the legacy unencrypted audio path.

The flags and the timestamp are clear and not covered by the SFrame AAD (which binds call, epoch, roster, sender, stream, sequence and timestamp values the receiver checks). They only steer what a relay forwards: receivers take the keyframe flag and the temporal layer from the fragment header inside the ciphertext, so a relay that rewrites them can only drop more or less, which it can do anyway. The relay learns each picture's layer, a coarse view of the frame structure comparable to WebRTC's dependency descriptor.

### Camera orientation

A phone camera hands over sensor-oriented (landscape) pixels with the display rotation and flip as `VideoFrame`
metadata, which a `VideoEncoder` does not carry. When every member announced `CallMediaFormat.Oriented` in its signed key
envelope, the sender encodes the sensor pixels as they are (a `new VideoFrame(frame, {rotation, flip})` with the inverse
orientation strips the metadata without a copy; an encoder must never see an orientation, since it refuses a change
without a reconfigure) and sends the orientation in the fragment header: version 2, byte 3 (quarter turns clockwise in
bits 0-1, a horizontal flip after the rotation in bit 2). The header is inside the SFrame plaintext, so the orientation is
encrypted and authenticated with the picture; a relay can neither read nor change it. The receiver applies it with one
transformed `drawImage` into a canvas of the displayed size. Turning the phone then changes only that byte: the sensor
raster, the encoder and the reference pictures stay, so it costs no keyframe. With any older member, or a browser that
cannot strip orientation, the sender redraws the frame upright first (as before), and a change in what the pixels have
baked in forces a keyframe. Upright pictures always use header version 1. Safari's frame-callback path paints the camera
element, which is upright already, and is unchanged.

### Congestion control on the WebSocket path

A call on TCP never loses media, so congestion shows as delay and as the relay's drops.

- **Relay.** Each receiver's queue sheds a stream's top temporal layer when it is 20% full, every enhancement layer at 40%, and the base layer (then everything until a keyframe) only when it overflows. A shed layer comes back only at a base-layer picture, so every forwarded picture's references were forwarded too. Every 250 ms (500 ms for audio) the relay tells each sender, per stream, about the worst receiver: its queue delay, the sender-to-relay queuing delay, the stream's share of what that receiver drained while backlogged, drops, base-layer losses and the layer limit (`MediaCongestion`).
- **Receiver.** Every 250 ms it adds its end-to-end queuing delay (one-way delay above its recent minimum, against the sender's capture clock) and what it received to its `MediaFeedback`.
- **Sender.** `MediaSendPacer` holds audio and video above the transport, sends audio first, keeps the connection's queue and the browser's WebSocket buffer short, and drops whole pictures (enhancement layers first). `SendRateController` turns the three views into one estimate (fast down, slow probing up, hysteresis), and `VideoRateLadder` into a picture size, frame rate and bitrate.

### Starting a picture at the size the link carries

A picture used to start at 240p15 and climb, which took 30-40 s to reach 1080p on a fast link. It now starts where the
link is known to carry it (`PictureStart`, `StartRate`):

- **Start probe.** When a participant's data channel first opens (while the call rings or connects), it sends about a
  second of paced `Padding` at 600, 1500, 3500 and 7500 kbit/s (200 ms each), stamped for transport feedback, and asks
  the relay to echo it. Feedback times the uplink, the echo the downlink. A step passes when the link delivered its
  rate without a queue building (25 ms) or loss (6%); the probe stops at the first step the uplink does not carry (that
  step's delivered rate is the link's) and stops asking for echoes at the first the downlink does not. A step holds
  back while the channel holds more than 40 ms of its rate, so audio never waits behind it. The relay echoes only to
  the sender, at most 2 MiB per connection, and only while that channel has under 32 KiB queued.
- **Receivers' downlinks.** Every `MediaFeedback` a receiver sends carries its probed downlink (or the browser's own
  ceiling for a slow connection). A sender takes the smallest exact one: the worst receiver.
- **The start.** Uplink measured (or, without one, the last call's settled rate on the same kind of network, or a
  540p middle picture), bounded by the worst receiver's downlink and the browser's network hints, under 85% of a
  measured limit. The ladder starts on the largest rung that rate clears, up to the user's preference; the first
  keyframe is that rung's.
- **Revisions.** For 10 s, while the path has shown no congestion, a measurement that arrives later moves the rate:
  a receiver's downlink down at once, a late probe up. A guess never does.
- **The ramp.** Until the path first shows congestion the estimate grows 60% a second after 500 ms of calm, and a
  ladder that has never come down jumps straight to the largest rung its budget held for 750 ms. A measured limit is
  the congestion point, approached slowly; the first step down returns the ladder to one rung at a time with backoff.
  Fast down is unchanged.

### The datagram path (WebRTC data channel through TURN)

A participant of an authenticated call may move its media off the WebSocket onto a WebRTC data channel to
the relay; the WebSocket keeps signalling, configuration and heartbeats, and takes media back whenever the
channel is not open. Only the pipe changes: the same Bolt frames, SFrame-encrypted end to end, one per
message, with DTLS as an extra hop layer.

- **Negotiation** rides the authenticated socket as `MediaTransport` frames: the participant asks; the relay
  mints short-lived ICE servers for it (`IBoltIceServerSource`, e.g. Cloudflare TURN) and announces a session;
  the participant offers, the relay answers with a relay-only peer; candidates trickle both ways.
- **The channel** is opened in band by the participant (DCEP), unordered, never retransmitted, binary, and
  carries at most 1150 bytes per message; the relay accepts only a channel with exactly those settings. (Pion
  keeps a pre-negotiated channel ordered and reliable on its sending side, so in-band opening is required.)
  The relay's endpoint is the `bolt-rtc` sidecar (Pion), driven by `Bolt.Rtc`.
- **Relay to participant.** The receiver's media lanes drain into the channel instead of the socket, never
  blocking: while the channel holds more than its SCTP window plus 16 KiB, media waits in the lanes (where audio
  still overtakes video and stale video is dropped). Frames too large for one message take the socket.
- **Participant to relay.** Only media and media feedback are accepted from a channel, routed exactly like the
  connection's own frames. Audio is de-duplicated per stream.
- **Congestion.** SCTP's loss-based window runs underneath with a 128 KiB floor on the relay's side; the
  relay's lanes and reports and the sender's delay-based controller set the rate, and the sender's pacer
  counts the channel's buffered amount as transport backlog, so loss is never reacted to twice.
- **Loss.** Each side reports the audio loss it sees once a second; above 1.5% the other side sends each
  audio frame with its predecessor (`MediaBundle`) while its own queue is short, until loss stays below 0.5%
  for 10 s.
- **Video fragments** are cut to fit one message on this path (4 KB on the socket); receivers take either.
- **Lifecycle.** A channel that does not open in 15 s or fails falls back to the socket, then retries with
  backoff; a network change restarts ICE in place; credentials are renewed with a fresh session before they
  expire, and a resumed socket negotiates its own.

### CallSignal Types

| Signal | Byte | Description |
|--------|------|-------------|
| Initiate | 0x01 | Start a call |
| Ring | 0x02 | Hub confirms callee found |
| Answer | 0x03 | Callee accepts |
| Reject | 0x04 | Callee declines |
| End | 0x05 | Either party hangs up |
| Hold | 0x06 | Pause media |
| Unhold | 0x07 | Resume media |
| AddParticipant | 0x08 | Group call: add member |
| RemoveParticipant | 0x09 | Group call: remove member |
| DirectOffer | 0x0A | Reserved for a future P2P upgrade; not operational |
| DirectAnswer | 0x0B | Reserved for a future P2P upgrade; not operational |

---

## Call State Machine

```
Initiating --> Ringing --> Active --> Ended
                       \-> Rejected   /\ (from Active or Held)
                       \-> Missed     Active <-> Held
```

- **Initiating -> Ringing:** callee is online and receives the signal
- **Ringing -> Active:** callee answers
- **Ringing -> Rejected/Missed:** callee rejects or 30-second timeout
- **Active <-> Held:** either party holds/unholds
- **Active/Held -> Ended:** either party ends

---

## Experimental Components

The sections below describe implementation primitives, not production-ready capabilities. Their behavior remains subject to the deployment containment above.

### Adaptive Bitrate

The receiver sends MediaFeedback every 250ms with quality metrics:

| Metric | Threshold | Action |
|--------|-----------|--------|
| Loss < 2%, jitter < 20ms | Maintain | No change |
| Loss = 0% for 5s, jitter < 10ms | Increase | +10% bitrate |
| Loss > 5% or jitter > 50ms | Decrease | -25% bitrate |
| Loss > 10% | Keyframe needed | Request IDR frame |

Bitrate floor: audio 16kbps, video 100kbps. Ceiling: originally negotiated bitrate.

### Forward Error Correction (FEC)

XOR-based parity. For every K source frames, one parity frame is generated.

| Track | Group Size (K) | Overhead | Recovery |
|-------|---------------|----------|----------|
| Audio | 4 | 25% | Any 1 lost frame per group |
| Video | 8 | 12.5% | Any 1 lost frame per group |

Enabled by default on TCP (WebSocket). Dynamic: enable when loss > 3%, disable when loss < 0.5%.

### Dynamic Throughput Maintenance

Multi-layer strategy to maintain target throughput under degrading networks:

| Layer | When Active | What It Does | CPU Cost |
|-------|-------------|-------------|----------|
| L1: Codec bitrate | Always | Reduce encoder bitrate | Low |
| L2: Resolution/FPS | Bandwidth < 50% target | Lower resolution, frame rate | Low |
| L3: LZ4 compression | Bandwidth < 70% target | Compress non-media frames | Very low |
| L4: Zstd compression | Bandwidth < 40% target | Higher compression ratio | Medium |
| L5: Audio-only | Bandwidth < 500 Kbps | Drop video entirely | None |

### Server-Side Media Hooks

```csharp
public interface IMediaProcessor
{
    bool Accepts(Guid callId, MediaType mediaType);
    ValueTask ProcessFrameAsync(Guid callId, Guid streamId,
        ReadOnlyMemory<byte> frameData, uint timestamp, uint sequenceNumber);
    ValueTask OnCallStartedAsync(Guid callId);
    ValueTask OnCallEndedAsync(Guid callId);
}

// Registration
services.AddBoltServer(options =>
{
    options.MediaProcessors.Add(new RecordingProcessor());
    options.MediaProcessors.Add(new TranscriptionProcessor());
});
```

---

## Codec Support

### Audio

| Codec | ID | Status | Notes |
|-------|----|--------|-------|
| Opus | 0x01 | Quarantined | Wire ID and partial codec path exist; end-to-end browser media is not release-qualified. |

### Video

| Codec | ID | Status | Notes |
|-------|----|--------|-------|
| H.264 | 0x02 | Yap encrypted groups only | Universal floor. Annex B, no decoder description. Hardware encode everywhere that matters. |
| VP9 | 0x03 | Yap encrypted groups only | ~20-30% fewer bits than H.264. Hardware encode on some phones; software up to 540p. |
| AV1 | 0x04 | Yap encrypted groups only | Best compression. Hardware encode is not widespread; software is allowed only up to 360p. |
| H.265 | 0x05 | Not negotiated | Licensing and patchy `VideoEncoder` support; not in the preference ladder. |

The browser probes every codec with `VideoEncoder.isConfigSupported` (and again with `hardwareAcceleration: 'require-hardware'`) at call setup, advertises what it can *decode* inside the end-to-end encrypted epoch envelope, and the sender picks the best codec every peer can decode. A codec is only preferred over the next one at a given height when this device reported hardware encode for it, or when the height is within that codec's software ceiling.

---

## Usage

The following snippets are design sketches and are not a supported production quick start. The audited high-level browser path does not currently complete encode-to-remote-decode media flow. Use these APIs only in isolated remediation tests until the quarantine is lifted.

### .NET Client

```csharp
var client = new BoltClient(serverUri, "my-service", "My App", options, logger);
await client.ConnectAsync(ct);

// Start a call
var callId = await client.StartCallAsync("other-service", video: true);

// Handle incoming calls
client.OnIncomingCall += async (info) =>
{
    await client.AnswerCallAsync(info.CallId);
};

// Send media frames
var stream = client.GetMediaStream(audioStreamId);
await stream.SendFrameAsync(opusEncodedAudio, isKeyframe: false);

// Receive media frames
await foreach (var frame in stream.ReadFramesAsync(ct))
{
    // frame.Data contains encoded audio/video
    // frame.IsKeyframe, frame.SequenceNumber, frame.Timestamp
}

// End call
await client.EndCallAsync(callId);
```

### Browser Client (TypeScript)

```typescript
import { BoltBrowserClient, AudioCodecHelper, VideoCodecHelper } from '@xframework/bolt-browser';

const client = new BoltBrowserClient('ws://bolt-hub:7000/bolt', 'browser-1', 'Browser');
await client.connect();

// Handle incoming calls
client.onIncomingCall = (callId, callerClientId) => {
    client.answerCall(callId);
};

// Start a call with media
const callId = client.startCall('other-user');
const audioStream = client.sendMediaConfig(crypto.randomUUID(), callId, true, 64);

// Encode audio via WebCodecs
const audio = new AudioCodecHelper();
await audio.initEncoder(48000, 1, 64000);
audio.onEncodedChunk = (data) => audioStream.sendFrame(data);

// Decode received audio
const decoder = new AudioCodecHelper();
await decoder.initDecoder();
audioStream.onFrame = (event) => decoder.decode(event.data, event.timestamp);
```

---

## Comparison vs WebRTC

### Intended architectural properties

| Feature | Details |
|---------|---------|
| Server-side media access | The design permits processing encoded frames, but processor filtering and cleanup require remediation |
| Unified protocol | The design shares Bolt framing and connections; isolation and head-of-line behavior require remediation |
| .NET-native | No 50MB libwebrtc dependency, pure managed code |
| Deployment model | Hub-routed media avoids STUN/TURN but does not currently provide a secure supported P2P fallback |
| Custom compression | Bolt-level LZ4/Zstd for non-media frames |
| Built-in SFU | Group calls without separate media server |

### Where WebRTC is ahead

| Feature | WebRTC | Bolt Media | Gap Level |
|---------|--------|------------|-----------|
| Peer-to-peer | ICE/STUN/TURN (automatic NAT traversal) | Hub-routed only (P2P planned, not coded) | Critical |
| Encryption | DTLS-SRTP (mandatory) | None (plaintext) | Critical |
| NACK retransmission | RTX (retransmit on request) | Not implemented | Critical |
| Congestion control | Google GCC (delay + loss based) | Throughput-based only | Important |
| Simulcast | 3 resolutions, SFU picks per receiver | Not implemented | Important |
| Opus in-band FEC | Built into codec | External XOR only | Important |
| Bandwidth probing | Periodic probes | Not implemented | Important |
| Packet loss concealment | Opus PLC | Not implemented | Moderate |
| SVC layers | VP9/AV1 spatial+temporal | Not implemented | Nice to have |
| DTX | Voice activity detection | Defined, not implemented | Nice to have |

### Overall Assessment

No defensible feature-parity percentage is currently available. WebRTC provides mature mandatory encryption, congestion control, NAT traversal, interoperability, and browser validation that Bolt Media has not demonstrated. Bolt Media remains experimental until its security, correctness, browser, loss/reordering, and soak gates pass.

---

## Implementation Phases

| Phase | Status | What |
|-------|--------|------|
| 1. Core protocol primitives | Experimental | Frame codecs, call state, and stream types exist; end-to-end correctness is not established |
| 2. Video, ABR, and FEC | Remediation required | Control loops, FEC grouping, sequence bounds, and cleanup are incomplete |
| 3. Group calls and server hooks | Remediation required | Membership, processor input, fanout budgets, and cleanup are incomplete |
| 4. QUIC/WebTransport datagrams | Not integrated | Helpers or frame recognition do not provide a working negotiated transport |
| 5. Browser client | Non-operational end to end | Stream registration, playback wiring, encryption isolation, and browser tests are incomplete |
| Security and reliability | Quarantined | Critical and high audit findings remain open |
| Tests | Insufficient for release | Primitive tests exist; adversarial, browser, multi-peer, and soak coverage is missing |

### Remaining Work

- Keep the server-enforced, disabled-by-default media gate closed until all release gates pass.
- Repair the bounded NACK/FEC/sequence pipeline and deterministic resource cleanup.
- Implement authenticated, transcript-bound, fail-closed per-call and group encryption.
- Repair browser stream registration, answer-side setup, timestamps, codec metadata, and playback.
- Validate congestion control, jitter, probing, simulcast, and hold behavior end to end.
- Either implement secure negotiated QUIC/WebTransport and P2P transports or keep them unadvertised.
- Pass real-browser, multi-peer, adversarial, loss/reordering, and long-running soak gates before release.

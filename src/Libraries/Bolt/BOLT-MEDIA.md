# Bolt Media Streaming — Voice/Video Calls over Bolt Protocol

## Overview

Bolt Media is an experimental extension of the Bolt binary RPC protocol for audio/video call research. The repository contains protocol and processing primitives, but the audited end-to-end media path is not production-ready and is not a replacement for WebRTC.

For standard XFramework module RPC, prefer the generated `[BoltHandler]` plus `IBoltRequest<TRequest, TResponse>` pattern documented in `BOLT.md`. Bolt Media is the specialized media-streaming layer, not the default pattern for CRUD or feature-command handlers.

**Current status:** quarantined pending remediation. The audit identified broken browser stream wiring, unbounded peer-controlled work, incomplete FEC/NACK behavior, unauthenticated key exchange, and lifecycle leaks. Existing unit tests do not establish a secure or operational end-to-end media path.

### Deployment Containment

Bolt Hub enforces `BoltConfiguration:MediaEnabled`, which defaults to `false` and is explicitly disabled in every XFramework Hub environment and Compose deployment. While quarantined, deployments must not override it, instantiate `BoltMediaClient` or `BoltMediaService`, expose media UI, route production media clients to the Hub, or advertise Bolt Media capability. Recognition of media frame types by the protocol does not constitute production enablement.

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
| MediaFeedback | 0x22 | 32 bytes (fixed) | Receiver reports loss, jitter, RTT |
| MediaKeyRequest | 0x23 | 17 bytes (fixed) | Request keyframe from sender |
| CallSignal | 0x24 | 22 bytes + payload | Call lifecycle signaling |
| FecFrame | 0x25 | 26 bytes + payload | XOR parity for error correction |

### MediaFrame Header (30 bytes)

```
[1:type=0x21] [16:streamId] [4:sequenceNumber] [4:timestamp] [1:flags] [4:payloadLen] [payload]
```

- **sequenceNumber** — monotonic per-stream, for ordering + gap detection
- **timestamp** — RTP-style media clock (48kHz for audio, 90kHz for video)
- **flags** — bit 0: keyframe, bit 1: end-of-picture, bit 2: marker, bit 3: FEC-protected, bit 4-5: priority, bit 6: drop-eligible, bit 7: compressed

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
| H.264 | 0x02 | Quarantined | Wire ID and partial codec path exist; end-to-end browser media is not release-qualified. |
| VP9 | 0x03 | Defined | Not yet integrated |
| AV1 | 0x04 | Defined | Not yet integrated |

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

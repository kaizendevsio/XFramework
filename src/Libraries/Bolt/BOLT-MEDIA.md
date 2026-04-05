# Bolt Media Streaming — Voice/Video Calls over Bolt Protocol

## Overview

Bolt Media extends the Bolt binary RPC protocol with real-time audio/video call support. Unlike WebRTC, Bolt handles media transport natively — no browser dependency, no STUN/TURN infrastructure, and full server-side access to media streams for recording, transcription, and AI processing.

**Current status:** Phase 1-5 implemented, critical bugs fixed, 37 tests passing.

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

The hub forwards each participant's media to all other participants (zero-decode, raw byte forwarding).

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
| DirectOffer | 0x0A | P2P upgrade offer |
| DirectAnswer | 0x0B | P2P upgrade accept |

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

## Features

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
| Opus | 0x01 | Supported | Default. Hardware accelerated via WebCodecs. |

### Video

| Codec | ID | Status | Notes |
|-------|----|--------|-------|
| H.264 | 0x02 | Supported | Default. Universal hardware acceleration. |
| VP9 | 0x03 | Defined | Not yet integrated |
| AV1 | 0x04 | Defined | Not yet integrated |

---

## Usage

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

### Where Bolt Media is ahead

| Feature | Details |
|---------|---------|
| Server-side media access | Hub sees every frame — recording, transcription, AI without extra infrastructure |
| Unified protocol | RPC + streaming + media on one connection |
| .NET-native | No 50MB libwebrtc dependency, pure managed code |
| Simple deployment | Single hub binary, no STUN/TURN servers |
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

**Bolt Media: ~40-50% feature parity with WebRTC for call quality.**

Bolt is ahead on server-side features (recording, transcription, AI hooks) and developer experience (.NET-native, unified protocol, simple deployment). The biggest quality gaps are P2P direct connections, media encryption, and NACK retransmission.

---

## Implementation Phases

| Phase | Status | What |
|-------|--------|------|
| 1. Core Protocol + 1:1 Audio | Done | 6 frame types, call state machine, BoltMediaStream |
| 2. Video + ABR + FEC | Done | AdaptiveBitrateController, FEC wiring, 256KB buffers |
| 3. Group Calls + Server Hooks | Done | SFU fan-out, IMediaProcessor, media tap channel |
| 4. QUIC Datagrams | Done | QuicDatagramHelper, QUIC server frame recognition |
| 5. Browser Client | Done | TypeScript client with WebCodecs (Opus + H.264) |
| Bug Fixes | Done | 8 critical+high bugs from audit |
| Tests | Done | 37 tests (protocol, FEC, jitter, calls, media exchange) |

### Remaining Work

- P2P direct connection upgrade (hub-first, then direct QUIC attempt)
- Media encryption (DTLS or application-level)
- NACK retransmission for video keyframes
- Delay-based congestion control
- Simulcast (multi-resolution encoding)
- Wire QUIC datagrams into BoltMediaStream
- Opus in-band FEC integration

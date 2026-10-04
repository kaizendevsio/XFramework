# Bolt SFrame adapter

This isolated browser adapter uses **sframe 2.0.0**, the existing RFC 9605 implementation, with **ring 0.17.14**, AES-256-GCM/SHA-512, and wasm-bindgen 0.2.100. Cargo.lock pins registry checksums. It does not implement a key exchange or enable calling. Source review and executable tests are not an independent security audit.

## Rebuild

Use Rust 1.88+, Node 22+, Clang with wasm32 support, and `rustup target add wasm32-unknown-unknown`. Install the pinned generator with `cargo install wasm-bindgen-cli --version 0.2.100 --locked`. Run `./build.ps1`, optionally `-ClangDirectory <directory containing clang.exe and llvm-ar.exe>` on Windows. The existing .NET Emscripten SDK includes suitable Clang tools. Cargo's default Windows native C compiler is not needed because tests run as WASM in Node.

The script tests the Rust binding as WASM, builds release WASM, generates the browser loader, tests the JS adapter, and updates published SHA-256 hashes. Commit only source, Cargo.lock, small generated browser assets and licenses; never target/ or a dependency tree. Application publish uses the checked-in static assets and does not need Rust. Changes to this adapter require this rebuild before application publish.

## Authenticated application context

**Upstream 2.0.0 nonempty metadata is deliberately unused.** Its frame API places metadata before the header in AEAD AAD. RFC 9605 section 4.4.3 and Appendix C.3 require header before metadata. The regression test reproduces this mismatch; the lower-level implementation passes the official AES-256 vector. Our adapter uses the conforming empty-metadata frame API. No upstream crypto code is patched.

An encrypted application payload consists of a two-byte big-endian context length, context UTF-8 bytes, and encoded Opus bytes. Context is the exact JSON array `['bolt-sframe-v1', callId, epochId, rosterBinding, senderId, streamId, sequence, timestamp]`. It is authenticated and encrypted by SFrame, and parsed only after authentication. The receiver checks expected context before recording the replay token or returning audio. This prevents a correctly encrypted packet moved to another stream/call/epoch from consuming that stream's replay state. Context is capped at 1024 bytes, Opus at 4096 bytes and wire payload at 5155 bytes.

## Compact frames (`bolt-sframe-v2`)

The legacy format above costs about 278 bytes per frame: the SFrame header with an 8-byte random KID (about 11 bytes), the 16-byte tag, and, inside the ciphertext, the two-byte length and about 250 bytes of JSON context (call UUID, epoch, 64-hex roster hash, the 75-character `yap-media-<call>-<credential>` sender, stream UUID, sequence, timestamp). For 50 audio packets a second that is about 110 kbps, more than 32 kbps Opus itself.

A compact frame is `header || ciphertext || tag`, at most 20 bytes more than the media (19 while the counter fits two bytes):

- **Header:** an ordinary RFC 9605 header whose KID is the format marker `1`, so it fits the config byte, followed by the minimal counter bytes. Sender KIDs below 8 are refused, so a legacy frame (which carries the sender's real KID) never looks compact.
- **AEAD:** AES-256-GCM with the full 16-byte tag. RFC 9605 shortens tags only in its AES-CTR + HMAC suites; ring provides none of them and a GCM tag is never cut. The key and salt are derived from the sender's real 64-bit KID exactly as for legacy frames.
- **Associated data:** `header || context`, the order RFC 9605 section 4.4.3 requires, sealed with the key's AEAD directly (the upstream frame API reverses it for nonempty metadata). The context is the same JSON with the label `bolt-sframe-v2`. It is never transmitted: both ends rebuild it from what they already know.
- **Counter and replay:** one counter per sender serves both formats, so no nonce repeats under a key; the receiver screens both formats in one 1024-frame window under the real KID and records a frame only after it authenticates.

**Negotiation.** Each member's authenticated epoch key envelope carries `Media` (`CallMediaFormat`): `2` reads compact frames. A sender sends them only when every remote member of the epoch announced `2` (`installEpoch({ ..., compact: true })`); receivers always take both formats. A call with an older client keeps legacy frames; the roster change that admits or removes a member renegotiates.

**What does not change.** Confidentiality (same keys and AEAD), integrity of media and context (every context field is still authenticated; only where it travels differs), binding to call, epoch, roster, sender, stream, sequence and timestamp (a frame moved to any other route fails its tag before it can touch that route's replay state), replay protection and epoch separation (fresh random base keys and KIDs per epoch, old keys dropped at installation). Sender authentication remains what it was: a sender key and KID come from that member's signed, encrypted envelope once per epoch, and no format signs frames. As in RFC 9605 section 9.5, a call member holding a sender's key could forge that sender's frames; the relay and server, which hold no keys, cannot. The format marker is no secret: the relay could already tell SFrame frames apart, and editing it fails the tag.

**Where compact frames depart from RFC 9605, and what that rests on.** The header's KID (the marker 1) is not the KID the key was derived from, so these frames are not interoperable as plain RFC 9605: a receiver must know the sender from elsewhere (the relay-stamped stream owner, checked against the epoch roster) and use that sender's key. This is safe only because every sender has its own independent base key, which `installEpoch` enforces ("Shared sender keys forbidden"): two senders can never share a key and nonce space under the same marker. The replay window is 1024 counters per sender, shared by audio and video; a video retransmission arriving more than 1024 of that sender's frames late is refused as too old. At 240p-720p rates that is several seconds, beyond the 1.5 s recovery window; at 1080p and above with large keyframes it can be less, and such a late retransmission is simply lost.

## Calling integration contract

Initialize the WASM module once. Create one `SFrameSession(callId, localSenderId)` per connection/call; use authenticated server-bound device/client IDs as sender IDs. Supply only keys recovered from verified expected-signer OpenPGP envelopes binding the tenant, thread, call, epoch, complete participant roster and sender. The rosterBinding is SHA-256 hex of that canonical authenticated roster. SFrame cannot supply directory trust itself.

Each epoch has one independently random 32-byte base key and unique decimal-string uint64 KID (at least 8) per sender. Each sender owns only its encryption key; peers receive its decryption key. `installEpoch` creates keys while paused; `activateEpoch` is called only after all current members acknowledge that exact epoch and roster. `pause` must run immediately when membership changes, before asynchronous key distribution. Fresh keys go only to retained accepted participants. Old receive keys are dropped at installation. Do not fall back to old epochs or plaintext, and do not call activate after failed distribution. A retry must not recreate a sender or reset its counter.

At most eight participants and 256 epochs are accepted. Duplicate sender/KID and same-epoch shared keys are rejected. KID history is bounded to this call; the authenticated key manager must supply fresh keys/KIDs across reconnects and never restore a prior sending key with counter zero. `dispose` frees WASM keys/receiver windows, clears session maps and permanently disables the object. The key manager must wipe its plaintext envelope/key arrays after installation; JavaScript garbage collection does not guarantee secure erasure. Static OpenPGP device keys do not give call-key distribution forward secrecy.

## Sources and license

The .NET bridge is `BoltSFrameInterop`, registered by `AddBoltMediaBrowser`, with explicit `MediaSecurityMode.AuthenticatedSFrame`. The older `EndToEndEncrypted` value remains unavailable and never invokes its unauthenticated ECDH exchange. `BoltMediaStream` passes timestamps into the new context-aware async encryption overloads before transmission and before jitter buffering/decoding. Inbound providers are installed synchronously before a configured stream becomes visible to receive handlers. The dedicated relay's `RequireEncryptedMedia` option rejects unencrypted configurations/frames and FEC; it overwrites configuration extension data with `SFR1:` plus the authenticated sender client ID. Encrypted packets are never fed to server media processors. This flag checks framing, while recipients cryptographically authenticate payloads.

The bridge admits at most 32 outstanding crypto operations, serializes epoch/key mutations with frame work, and rejects results overtaken by a pause or key change. Providers are bound to their original call, so a disposed call's stream cannot use a replacement call's keys. All-member acknowledgment and immediate server-side pause/removal ordering still belong to the calling coordinator; the library cannot infer membership changes from ciphertext.

- [RFC 9605](https://www.rfc-editor.org/rfc/rfc9605.html), particularly sections 4.4, 9.1, 9.3 and Appendix C.3.
- [sframe 2.0.0](https://github.com/TobTheRock/sframe-rs/tree/v2.0.0), MIT or Apache-2.0.
- [ring 0.17.14](https://github.com/briansmith/ring/tree/0.17.14), ISC/MIT/OpenSSL licenses.
- [wasm-bindgen](https://github.com/wasm-bindgen/wasm-bindgen/tree/0.2.100), MIT or Apache-2.0.

Published license files accompany the generated assets. Full session authorization, malicious-directory resistance, epoch acknowledgment races, all-member rotation and real multi-device 128 kbps audio tests remain integration gates; adapter unit tests alone do not satisfy them.

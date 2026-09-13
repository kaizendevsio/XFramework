# Bolt SFrame adapter

This isolated browser adapter uses **sframe 2.0.0**, the existing RFC 9605 implementation, with **ring 0.17.14**, AES-256-GCM/SHA-512, and wasm-bindgen 0.2.100. Cargo.lock pins registry checksums. It does not implement a key exchange or enable calling. Source review and executable tests are not an independent security audit.

## Rebuild

Use Rust 1.88+, Node 22+, Clang with wasm32 support, and `rustup target add wasm32-unknown-unknown`. Install the pinned generator with `cargo install wasm-bindgen-cli --version 0.2.100 --locked`. Run `./build.ps1`, optionally `-ClangDirectory <directory containing clang.exe and llvm-ar.exe>` on Windows. The existing .NET Emscripten SDK includes suitable Clang tools. Cargo's default Windows native C compiler is not needed because tests run as WASM in Node.

The script tests the Rust binding as WASM, builds release WASM, generates the browser loader, tests the JS adapter, and updates published SHA-256 hashes. Commit only source, Cargo.lock, small generated browser assets and licenses; never target/ or a dependency tree. Application publish uses the checked-in static assets and does not need Rust. Changes to this adapter require this rebuild before application publish.

## Authenticated application context

**Upstream 2.0.0 nonempty metadata is deliberately unused.** Its frame API places metadata before the header in AEAD AAD. RFC 9605 section 4.4.3 and Appendix C.3 require header before metadata. The regression test reproduces this mismatch; the lower-level implementation passes the official AES-256 vector. Our adapter uses the conforming empty-metadata frame API. No upstream crypto code is patched.

An encrypted application payload consists of a two-byte big-endian context length, context UTF-8 bytes, and encoded Opus bytes. Context is the exact JSON array `['bolt-sframe-v1', callId, epochId, rosterBinding, senderId, streamId, sequence, timestamp]`. It is authenticated and encrypted by SFrame, and parsed only after authentication. The receiver checks expected context before recording the replay token or returning audio. This prevents a correctly encrypted packet moved to another stream/call/epoch from consuming that stream's replay state. Context is capped at 1024 bytes, Opus at 4096 bytes and wire payload at 5155 bytes.

## Calling integration contract

Initialize the WASM module once. Create one `SFrameSession(callId, localSenderId)` per connection/call; use authenticated server-bound device/client IDs as sender IDs. Supply only keys recovered from verified expected-signer OpenPGP envelopes binding the tenant, thread, call, epoch, complete participant roster and sender. The rosterBinding is SHA-256 hex of that canonical authenticated roster. SFrame cannot supply directory trust itself.

Each epoch has one independently random 32-byte base key and unique decimal-string uint64 KID per sender. Each sender owns only its encryption key; peers receive its decryption key. `installEpoch` creates keys while paused; `activateEpoch` is called only after all current members acknowledge that exact epoch and roster. `pause` must run immediately when membership changes, before asynchronous key distribution. Fresh keys go only to retained accepted participants. Old receive keys are dropped at installation. Do not fall back to old epochs or plaintext, and do not call activate after failed distribution. A retry must not recreate a sender or reset its counter.

At most eight participants and 256 epochs are accepted. Duplicate sender/KID and same-epoch shared keys are rejected. KID history is bounded to this call; the authenticated key manager must supply fresh keys/KIDs across reconnects and never restore a prior sending key with counter zero. `dispose` frees WASM keys/receiver windows, clears session maps and permanently disables the object. The key manager must wipe its plaintext envelope/key arrays after installation; JavaScript garbage collection does not guarantee secure erasure. Static OpenPGP device keys do not give call-key distribution forward secrecy.

## Sources and license

- [RFC 9605](https://www.rfc-editor.org/rfc/rfc9605.html), particularly sections 4.4, 9.1, 9.3 and Appendix C.3.
- [sframe 2.0.0](https://github.com/TobTheRock/sframe-rs/tree/v2.0.0), MIT or Apache-2.0.
- [ring 0.17.14](https://github.com/briansmith/ring/tree/0.17.14), ISC/MIT/OpenSSL licenses.
- [wasm-bindgen](https://github.com/wasm-bindgen/wasm-bindgen/tree/0.2.100), MIT or Apache-2.0.

Published license files accompany the generated assets. Full session authorization, malicious-directory resistance, epoch acknowledgment races, all-member rotation and real multi-device 128 kbps audio tests remain integration gates; adapter unit tests alone do not satisfy them.

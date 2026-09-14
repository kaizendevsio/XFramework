# Yap encrypted messaging, recovery and group voice

Decision date: 14 September 2026. Status: the in-house implementation has passed
the desktop browser proofs listed below; remaining integration checks continue.
This document describes the prepared
release; it does not claim deployment, physical mobile verification or an
independent security audit. Production encrypted group calling remains gated
until the checks below pass. The previous trusted-server voice slice is a
different security mode and must never be presented as E2EE.

## Architecture and cryptographic choice

Yap keeps its existing XFramework modules and Bolt transport. There is no Matrix
backend or new encryption service. Persistence follows the single physical
PostgreSQL database with schema-per-module boundaries.

- IdentityServer owns immutable account root public keys, signed device rosters,
  device approval/revocation records and opaque encrypted recovery archives.
  Bounded whole-roster changes use compare-and-swap revisions. The server enforces
  authorization and immutable records; browsers independently verify signatures.
- Communications owns conversation membership and routing, stores opaque signed
  message envelopes, and stamps the accepted sender device/directory revision.
  New messages validate the recipient account set and directory revisions before
  acceptance. Already accepted retries retain their original envelope and stamp.
- Storage persists encrypted attachment objects with conversation authorization.
  File contents, original filenames and MIME types stay inside encrypted data.
- Yap's browser library uses the pinned, unmodified **OpenPGP.js 6.3.1** bundle
  for RFC 9580 encryption and signatures. Private keys remain in account/tenant
  scoped IndexedDB. No plaintext fallback exists in the encryption library.
- The dedicated Bolt instance inside the Yap host relays authenticated browser
  traffic over same-origin WSS. Encrypted voice uses the **sframe 2.0.0** RFC 9605
  implementation compiled to WASM, backed by ring 0.17.14. The existing server
  gateway, rather than the private shared Hub, authorizes each participant.

The choice is explicit: static OpenPGP device keys provide **neither forward
secrecy nor post-compromise security**. Later compromise of a device's private
decryption keys can expose previously recorded ciphertext addressed to those
keys, including recorded call-key envelopes. SFrame does not remove that key
distribution limitation. Do not describe this as a ratchet or claim the security
properties of protocols that provide forward secrecy.

## Trust and signed context

An account master signing key approves distinct signing/encryption keys for
each device. Root-signed rosters bind the tenant, account, revision and complete
device list, including revocations. Clients pin the root fingerprint and highest
validated roster revision/digest, rejecting replacement, rollback and conflicting
same-revision manifests. First contact uses trust on first use until users compare
fingerprints through another trusted channel; a fresh unverified contact cannot
detect an initial malicious directory substitution on its own.

Each encrypted message has exactly one verified expected-device signature. Its
signed context binds tenant, thread, message, sender, reply metadata and recipient
roster. The server-accepted sender device/revision is compared with the signed
record. This allows old history from a subsequently revoked device while rejecting
new backdated envelopes from that device. Recipient revision checks close the
gap between fetching a directory and sending to a newly revoked recipient.

Message bodies, attachment descriptors and file bytes are encrypted. Routing,
participants, timing and sizes remain visible. An opaque attachment is named
`attachment.pgp`; a voice recording is named `voice.pgp`, both with MIME type
`application/octet-stream`. **Voice-versus-other-attachment category is visible
metadata** so the server can enforce the conversation's separate voice and
attachment switches. Profile/group avatar images are public to their authorized
audience through the existing image feature; they are not encrypted chat files.

Old server-visible messages do not acquire retroactive confidentiality. Search
of encrypted text uses locally decrypted history; the server cannot search its
plaintext. Server image transcoding cannot process encrypted attachments, so
preview conversion, including HEIF compatibility, runs on the recipient device.

## New devices and recovery

The first enrolled device creates the account root and fresh device keys. Pending
keys are saved before directory publication; a lost response does not regenerate
the identity. The owner device keeps the master signing key. Recovery archives
contain that root and historical decryption material encrypted under a random
256-bit recovery key. The recovery key remains stable across backup updates,
never goes to the server, and is displayed only after an explicit user request.
Background backup repair must not reveal the key or block the inbox.

There are two different workflows:

1. **Approve an additional device.** The new browser creates fresh keys and
   shares a public proposal with the owner device. One root-signed roster update
   revokes the owner's old device keys and adds fresh owner keys plus the proposed
   device. The encrypted approval transfers only retired history keys; it never
   copies another active device's private key or the account master key. The new
   device can read the owner's earlier history, while future ciphertext uses the
   fresh active keys. Pending rotation/results survive retry; owner activation
   waits for the confirmed roster. A lost publication response is reconciled
   against the saved signed roster. Each approval consumes two records within
   the 16-record roster bound, including tombstones. The owner then saves an
   updated recovery archive. Additional devices cannot act as master owners.
2. **Recover with the recovery key.** The browser verifies the encrypted archive
   against the immutable account root, creates a fresh device identity and revokes
   all previously active devices in one roster update. Restored device keys are
   history-only. It then publishes the new directory and updated encrypted backup.
   If publication completed before a lost response, retry accepts that persisted
   fresh identity rather than creating another one.

Device revocation stops future messages; it cannot erase content already read or
downloaded. Compromise of the master key or recovery secret is account-authority
compromise and is not repaired by an ordinary device revocation. Losing every
usable trusted device and the recovery key makes encrypted history unrecoverable;
login or password reset alone cannot decrypt it. Browser-origin script compromise
can access unlocked local keys and is outside the E2EE protection boundary.

## Attachment streaming and recovery from crashes

The small-byte API is bounded at 64 MiB. The stream API supports up to 4 GiB of
plaintext with bounded AEAD chunks and a separate 16 MiB ciphertext-overhead
allowance. Uploads stage encrypted bytes in OPFS and use the existing chunked
Storage upload endpoints; they do not materialize the full file in JS memory.

Downloads write only to quarantined OPFS files until full AEAD, expected-signer
and message-context verification completes. Only then is the verified file copied
to its final location. A ready sidecar is written **after final close**, binding
the account, signed context digest and file size. Existence alone never proves
verification: missing/malformed markers and partial files force a retry. Startup
cleans abandoned temporary files. A valid verified cache can be used offline
without fetching the sender directory again. Concurrent readers share a single
decryption operation.

Outbox ciphertext and its sender/attachment context must be persisted before a
network request. Directory conflicts require confirming a message was not already
accepted before changing ciphertext; response-loss retries must preserve the
original randomized envelope. Attachment contexts cannot be silently overwritten
when a message retry uses a newer device roster.

## Encrypted group voice

Calls support up to eight accepted participants with a default **128 kbps Opus**
encoder target. WebCodecs is preferred; the managed Concentus fallback covers
Opus encoding/decoding where browser codecs are unavailable. AudioWorklet PCM,
independent bounded peer decoders, capture cancellation and cleanup remain shared.

Each sender/epoch has a fresh random SFrame base key and unique key ID. Key and
acknowledgment envelopes are signed OpenPGP messages encrypted to the exact
approved participant devices. Context binds the call, epoch, authenticated sender,
recipient and complete roster. On membership change, media pauses before key
distribution and resumes only after all current participants acknowledge the
installed epoch. Removed participants do not receive replacement keys. Frame
context and replay checks run before Opus decoding; stale callbacks cannot
reactivate a superseded epoch.

The SFrame adapter uses the upstream empty-metadata frame API because its tests
found nonempty metadata ordered differently from RFC 9605. Application context
is carried **inside the encrypted payload** and checked after authentication; no
upstream cipher implementation was patched. See the detailed
[adapter contract](../../../src/Libraries/Bolt/Bolt.Media.Browser/sframe/README.md).

The relay must require encrypted media and reject plaintext configurations/frames;
server media processors must not decode encrypted payloads. The quarantined legacy
ECDH exchange stays unavailable. No silent fallback to trusted-server audio is
allowed when the encrypted mode is selected. The WSS route requires HTTPS, exact
Origin, session-bound single-use tickets and current membership. The supported
gateway remains one Yap host instance; restarts end calls, and WSS/TCP network
behavior still needs mobile measurement.

## Verification and deployment gates

Executable coverage includes actual OpenPGP multi-recipient encryption, signed
context replay/tamper rejection, device approval/rotation/revocation, fresh recovery,
an 80 MiB bounded stream, OPFS crash markers and account isolation. Focused .NET
tests cover explicit secret reveal, late account-switch callbacks, directory
response loss and backup repair. SFrame tests cover official vectors, replay,
context/epoch isolation and lifecycle bounds. These are implementation evidence,
not an independent cryptographic audit.

Completed desktop Chrome fixture proofs on 14 September 2026 include:

- Three independent accounts exchanged encrypted messages through the actual
  client/BFF path. A 7 MB JPEG reached every recipient and opened in the photo
  viewer; an encrypted MOV attachment played in the browser.
- Encrypted edits updated plaintext across all three accounts. With file links
  delayed three seconds, a recipient skeleton appeared before links, then all
  three clients displayed the preview automatically. Raw server content remained
  a generic placeholder plus the armored envelope.
- Additional-device approval rotated the owner's device identity and transferred
  readable history. Recovery created a fresh device and revoked old devices.
- An **80 MiB (83,886,080-byte)** file completed actual browser encrypted upload,
  download and verification. Original and recovered files both have SHA-256
  `3A947FE28882D03D4D8948E94D4A870BF4ACB1809B4D9D78090CDBF323FD1AAC`.
  This is browser-path evidence beyond the bounded-stream unit test, not a
  completed 4 GiB stress test.
- Three-party voice produced nonzero decoded audio from both peers on every
  participant. Runtime probes observed 128,000 bits/s Opus encoder configuration
  and real SFrame encryption/decryption in shared epoch 3. Muting stopped that
  sender's received-frame growth while another sender continued; unmuting and
  leaving preserved audio among the remaining participants.
- Recovering an account during a call revoked its old device. The authorization
  lease removed that device, and the two remaining browsers activated epoch 4
  with a new matching roster binding and continued nonzero audio. The removed
  device's counters stayed fixed during the follow-up observation; neither
  remaining browser reported a crypto failure.

The lasting [browser verification report](yap-encryption-browser-verification.md)
records the message/media checks, frame counters, bitrate, mute/departure and
recovery revocation. The final focused suite passed 173 tests: 49 client .NET,
103 host .NET and 21 JavaScript. These were disposable HTTPS fixtures with isolated
desktop Chrome profiles, not a public deployment or physical phones.

Before enabling the prepared release, the owner task must still:

- Apply and verify Identity/Communications migrations and authorization/CAS tests.
- Preserve the completed encrypted-edit and delayed-file-link browser behavior
  and retain lost-response and stale-directory outbox regression coverage.
- Preserve the recorded encrypted group-voice proofs when changing the gateway,
  key exchange or media pipeline. Verify release configuration, fallback codec
  behavior and teardown for the shipped build; primitive replay/tamper tests and
  earlier trusted-TLS runs do not substitute for encrypted browser-path evidence.
- Confirm production configuration and constructor wiring select the explicit
  encrypted mode only after those checks. Keep shared Hub media and legacy ECDH
  disabled, and confirm the public same-origin Funnel/WSS route.
- Check the changed photo/video flows, batch attachments, message popups and
  bounded history window. Desktop Chrome at a mobile viewport is not physical
  iOS Safari or Android Chrome verification; document that remaining device gap.

Version 1.3.0 release notes describe this prepared batch. A source version bump is
not deployment evidence. The earlier
[trusted-server relay decision](yap-voice-trusted-server-relay.md) remains historical
context for the prior mode. The [client integration contract](../../../src/Presentation/XFramework.Yap.Client/ENCRYPTION.md)
contains the exact browser API and bounds.

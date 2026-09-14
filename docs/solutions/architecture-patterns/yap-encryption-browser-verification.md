# Yap encrypted browser verification

Verified on 14 September 2026 in disposable HTTPS Yap fixtures with isolated
desktop Chrome profiles. This report preserves sanitized integration evidence
for the prepared 1.3.0 release. It is not evidence of public deployment, physical
iOS/Android compatibility or an independent security audit. No passwords, private
keys, recovery secrets, message ciphertext or recorded audio are included here.

## Messaging, files and device lifecycle

- Three independently authenticated accounts exchanged encrypted messages
  through the actual browser client/BFF path. Raw server message content remained
  a generic placeholder plus the armored encrypted envelope.
- Editing an encrypted message updated the displayed plaintext across all three
  accounts. The server retained the generic content and armored envelope.
- A 7 MB JPEG reached every recipient and opened in the photo viewer. An
  encrypted MOV attachment played in the browser.
- With attachment links deliberately delayed by three seconds, the recipient
  displayed an image skeleton before links became available. All three clients
  subsequently displayed the preview automatically, without a reload.
- An 80 MiB (83,886,080-byte) file completed actual browser encrypted upload,
  download and verification. The original and recovered files had the same
  SHA-256: `3A947FE28882D03D4D8948E94D4A870BF4ACB1809B4D9D78090CDBF323FD1AAC`.
  This exercises the browser streaming path beyond the 64 MiB byte API; it does
  not establish successful transfer at the full 4 GiB limit.
- Additional-device approval rotated the owner's device identity and transferred
  readable history. Recovery created a fresh device and revoked former devices.

Local, ignored evidence includes `artifacts/yap/encrypted-80mb-fixture.bin` and
`artifacts/yap/encrypted-downloads/encrypted-80mb-fixture.bin`. Their byte counts
and hashes were checked directly. These names identify local evidence; this
report does not require those files to exist in a repository checkout.

## Three-party encrypted voice

The call used approved device directories, signed OpenPGP control envelopes,
SFrame and the hosted Bolt media gateway. Each participant decoded nonzero audio
from both other participants. This was measured in playback instrumentation,
rather than inferred from the call status text.

The follow-up fixture run at 23:58:16–17 UTC on 13 September observed:

| Browser | Opus encoder target | SFrame encrypted frames | SFrame decrypted frames | Epoch | Participants |
|---|---:|---:|---:|---:|---:|
| A | 128,000 bits/s | 411 | 895 | 3 | 3 |
| B | 128,000 bits/s | 463 | 887 | 3 | 3 |
| C | 128,000 bits/s | 476 | 916 | 3 | 3 |

All three activated the same authenticated roster binding and reported no
encryption/decryption failures in those snapshots. The bitrate is the actual
`AudioEncoder.configure` value, not a measured network throughput. The probe's
unused `wireFrames` and `plainWireFrames` placeholders were not instrumented and
must not be interpreted as measurements.

In the preceding mute/departure run, A's received stream on B stayed at 6,636
frames while A was muted; C's stream grew from 6,642 to 7,381. Unmuting restored
A's audio. C then left through the UI, while A and B exchanged another 854/852
decoded frames over approximately 17 seconds. C's stream counters stayed fixed.
The remaining calls were ended through their UI.

## Recovery revocation during a call

An independent browser recovered account A, creating a new device and revoking
the former one. The authorization lease removed old A from the active call, its
overlay closed, and it never installed the next epoch. B and C both activated
epoch 4 with exactly two participants and a matching new roster binding. Their
SFrame exchange and decoded nonzero audio continued without reported crypto
failures.

In a stable follow-up roughly 27 seconds later, revoked A remained at 2,048
encrypted / 4,481 decrypted frames with decoded counters unchanged. B added
1,338 decrypted frames and C added 1,357; their streams from A remained fixed at
2,046 frames. The remaining calls were then ended through the UI. This exercises
directory revocation, server lease removal, roster change, signed encrypted key
distribution, acknowledgment and epoch reactivation through actual clients.

Local evidence names are `artifacts/group-audio-browser-verification.md`,
`artifacts/group-audio-encryption-verification.md`,
`group-audio-secure-{a,b,c}-{before-recovery,after-recovery,stable-revocation}.json`
and the `group-audio-muted-*` / `group-audio-left-*` snapshots. The lasting results
are recorded above because those local artifacts are not tracked source files.

## Automated checks and limits

The final focused regression run passed **173 tests**: 49 client .NET tests,
103 host .NET tests and 21 JavaScript tests. Coverage includes account-switch and
response-loss handling, explicit recovery-secret reveal, signed context and
revocation checks, bounded streams, OPFS verification markers and cache isolation.
The test total complements the browser observations; it does not imply every
platform or maximum-size transfer was exercised.

These fixtures used desktop Chrome test audio input, not physical phone
microphones or speakers. Physical Safari/Android behavior, public Funnel/WSS
deployment, the final deployed configuration, full 4 GiB transfers and an
independent cryptographic audit are not established here. Static OpenPGP device
keys provide neither forward secrecy nor post-compromise security, including
for the envelopes carrying call keys.

See the [architecture decision](yap-encrypted-recovery-decision.md) and
[client API contract](../../../src/Presentation/XFramework.Yap.Client/ENCRYPTION.md)
for trust boundaries, recovery behavior and deployment gates.


## Deferred recipient verification ? 14 September 2026 (1.3.5)

Using the real HTTPS Yap host/browser code with isolated Chrome profiles and
three fixture accounts, two accounts enrolled while the third had no directory.
The sender sent text and a 7,080,834-byte JPEG. Both displayed `Sent`, with a
separate one-member setup indicator. The ready recipient read the text and
rendered the decrypted 1440 ? 1080 image before the third account signed in.
After the third account's first sign-in automatically enrolled it, the sender's
background catch-up cleared the pending count; the third account read the text
and rendered the same image. Local screenshots are retained under
`artifacts/yap/deferred-ready-before-enrollment.png` and
`artifacts/yap/deferred-late-received.png`. These are fixtures, not real-user sends
or physical iOS/Android validation.

A real OpenPGP streaming test separately verified a 7 MiB encrypted attachment:
the late account failed without the protected attachment key, then opened the
unchanged ciphertext using the key from a signed encrypted message. Corrupt
ciphertext, wrong context and wrong keys failed without committing plaintext.
A PostgreSQL test ran catch-up concurrently on independent connections and
verified one success, one conflict, and one outbox event. Service tests cover
omitted ready recipients, frozen original audience, sender-only catch-up,
create-response-loss retries, stale catch-up after editing, and suppression of
Delivered/Read receipts for deferred members.

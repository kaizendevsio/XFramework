# Browser encryption integration

`wwwroot/encryption.mjs` exports `encryption`. The interop facade at
`window.yap.encryption` starts a dedicated module worker, which owns OpenPGP,
key derivation, signing, verification and the existing IndexedDB key store.
`crypto-rpc.mjs` transfers streams through pull-based MessagePorts for mobile
compatibility; only one chunk per read is in flight. Attachment sinks remain
quarantined until signature verification commits them. Worker failures reject
pending work; later calls recreate the worker using the existing keys, with no
plaintext or main-thread crypto fallback. Blazor rendering stays on the UI
thread; this does not enable the experimental .NET WASM threads runtime.

The vendored OpenPGP.js 6.3.1
bundle is unmodified; its npm SHA-512 integrity was checked before copying.
It uses RFC 9580 authenticated encryption and signatures, with 256 KiB AEAD
chunks. No plaintext fallback exists in this library.

This is Yap's in-house integration with IdentityServer, Communications, Storage
and Bolt; it does not require Matrix or a separate encryption service. The
implementation is being prepared for release 1.3.0. Source and automated coverage
do not establish deployment, physical iOS/Android compatibility or an independent
security audit. See the [architecture and deployment gates](../../../docs/solutions/architecture-patterns/yap-encrypted-recovery-decision.md).

## Account/device lifecycle

Scope is `{tenantId, credentialId}` (GUID D strings), or `tenantN:credentialN`.
All persistence and trust pins are bound to this account and tenant.

- `status(scope)` returns `enrolled`, `approved`, `deviceId`, `rootFingerprint`,
  `directoryRevision`, `canApproveDevices`, and `verifiedContacts`.
- `initialize(scope,account)` persists a pending identity before returning
  `{directory,recoveryArchive,recoveryKey}`. Repeated calls retain the identity.
  Publish directory with expected revision 0, then call `acceptDirectory`.
  `account` is the server's checked answer about this account, gathered *before*
  anything is created: `{kind:'account-identity',checked,directory,recoveryArchive}`.
  A root is minted only for a checked account holding neither a directory nor an
  archive. A published identity reports "this account already has encrypted
  messages set up"; an unchecked one - offline, 5xx, a caller that never asked -
  reports that it could not check. Neither creates anything, and neither deletes
  anything: a device that stays locked keeps every key it already had. An
  identity this account never confirmed is likewise never offered in place of the
  published one. Creating a replacement root remains available, deliberately and
  with its own confirmation, through `prepareReset`/`confirmReset`.
- `acceptDirectory(scope,directory)` validates the full signed roster, approval
  and revocation records. It pins the root fingerprint, highest revision and
  manifest digest. Root changes, rollback and conflicting same-revision records
  fail. Own device approval becomes active only after accepting its directory.
- `verifyFingerprint(scope,credentialId,fingerprint)` marks an existing matching
  pin verified. The user must compare fingerprints through another trusted path.
- `proposeDevice(scope)` creates a fresh keypair and returns a public proposal.
- `approveDevice(scope,proposal,directory)` returns
  `{alreadyPublished,expectedRevision,directory,approval}`. Publish its directory using CAS unless `alreadyPublished` is true, then
  give the approval object to the new device through the explicit approval flow.
  In that same roster revision it revokes the owner's old device keys and adds
  fresh owner keys. Pending keys/results are persisted before publishing; retry
  returns the exact staged result. `acceptDirectory` activates replacement owner
  keys only after the server confirms the roster. Refresh the owner's C# status
  and save its updated recovery archive after confirmation. Two device slots are
  consumed per additional-device approval, within the 16-record lifetime bound.
- `importApproval(scope,approval,directory)` validates the directory against the
  approving device's fingerprint and decrypts its root-signed transfer. That
  transfer contains only retired history keys. **Active device keys and the
  account master key are never copied to an additionally approved device.**
- `revokeDevice(scope,deviceId,directory)` returns a CAS directory update.
- `exportRecovery(scope)` returns `{recoveryArchive,recoveryKey}`. The random
  256-bit recovery key is stable across exports; store the matching updated
  encrypted archive on the server. Never send the recovery key to the server.
- `recovery(scope,recoveryKey,recoveryArchive,directory)` validates account/root
  binding and signatures, generates a fresh device, and revokes every previous
  active device. Returns `{expectedRevision,directory,recoveryArchive,recoveryKey}`.
  Publish the directory and accept it, then save the new archive. Recovered old
  device keys are history-only; they are never reused as active identities.

The account owner device holds the master signing key. Revoking a device does
not protect an account whose master key or recovery secret has been stolen.
Additional devices cannot approve others or export the owner's recovery archive.
Approving a device transfers history readable using the owner's retired keys.
The owner key rotation occurs atomically with approval, so those transferred
keys cannot decrypt future messages addressed to the fresh active devices.
Never copy the private key of another active device.

`EnsureAsync` is the one place that decides between the three cases, in a fixed
order, inside the account lock that just read the directory. It reads the
published directory; on 404 it also reads the recovery archive, and only when the
server holds neither does it enroll. When the account has an identity this device
cannot yet read, it finishes the unlock the sign-in already paid for -
`passwordRestore`, using the export key from that exchange - before anything
else, so history no longer depends on whether sign-in or synchronization reached
the module first. If that cannot unlock, it publishes nothing, records `Locked`
with the reason the module gave, and leaves the local device request in place so
recovery-key restore, trusted-device approval and start-fresh all remain
reachable from Settings. A directory read that fails for any other reason -
offline, 5xx, a changed account - propagates: an unknown answer is never read as
"this account has no identity".

The C# coordinator reconciles an uncertain directory publication by fetching the
server's signed roster and comparing it with the persisted pending result. A
confirmed recovery must reuse its fresh identity when repairing a failed backup,
rather than recovering again and rotating another time. Backup repair does not
display the recovery key: only the explicit Show action reveals it. Account
switches invalidate pending callbacks, and leaving settings hides revealed keys.

## Directory wire records

Directory: `{tenantId,credentialId,revision,rootPublicKey,roster,devices}`.
Each device: `{deviceId,signingPublicKey,encryptionPublicKey,approval,revocation}`.
The roster is an attached signed OpenPGP message binding version 1, kind
`device-roster`, account/tenant, revision, root fingerprint and sorted complete
device records. Approval binds account/tenant, device ID, both public-key
fingerprints and approved revision. Revocation binds account/tenant, device ID
and revoked revision. All are signed by the immutable account root.

## Messages, attachments and call-key envelopes

Context always includes `tenantId,threadId,messageId,senderId,kind` and all
application metadata whose replay must be prohibited, such as `parentId` and
`isThreadReply`. All supplied fields are signed and compared exactly on decrypt.
Do not include undefined values. The library internally adds the real device ID,
sender directory revision, and complete recipient directory revision/digest plus
selected recipient device IDs.

- `encrypt(scope,context,payload,directories)` returns armored ciphertext, max
  256 KiB. Payload is a JSON value, not a pre-serialized string.
- `decrypt(scope,context,envelope,senderDirectory)` returns that JSON value only
  after verifying exactly one signature against its approved sending device.
- `encryptBytes/decryptBytes` use the same arguments for `Uint8Array`, return
  binary bytes, and cap plaintext at 64 MiB.
- `encryptToDevices(scope,context,payload,directories,recipientDeviceIds)` limits
  recipients to those exact approved device IDs. Full signed directories,
  including the sender directory, are still required. Missing/revoked IDs fail.
- `encryptStream(scope,context,source,directories)` returns a binary ciphertext
  `ReadableStream<Uint8Array>`, with a 4 GiB plaintext cap and no full-file buffer.
- `decryptStream(scope,context,source,senderDirectory,sink)` takes a quarantine
  sink with async `write(chunk)`, `commit()`, and `abort()`. It returns the result
  of `commit()`. Writes must remain inaccessible to preview/UI until commit.
  Commit occurs only after consuming and authenticating the entire stream,
  signature and context. Errors call abort; the caller deletes temporary OPFS
  content there. A browser crash also requires cleaning abandoned temp files on
  the next launch. Do not use this API with a visible/streaming playback sink.

Optional `context.senderDeviceId` (or `expectedSenderDeviceId`) must match the
actual signing device, useful for call rosters. Optional
`context.expectedSenderDirectoryRevision` must match the signed sender revision.
**The server must stamp the validated current sender device/revision when a new
message is accepted and provide that stamp for decryption.** This distinguishes
valid older history from a revoked device forging a new backdated envelope.
The server also checks the recipient account set and directory revisions on a
new send. Persist ciphertext and its attachment contexts before a request; a
response-loss retry must not replace an already accepted randomized envelope.

### Deferred recipients (1.3.5)

New messages freeze their audience as membership IDs. The server requires every
currently configured member in the submitted recipient directory snapshot; only
members without active devices may be deferred. Accepted ciphertext is `Sent`;
the sender sees the remaining setup count separately. A deferred member cannot
produce Delivered or Read receipts until access has been completed.

An approved sender device checks up to 20 pending messages per synchronization,
across its active conversations, rotating pages. When original members enroll,
it decrypts and re-encrypts the message for ready members of that original
audience. Newly added or removed-and-readded memberships are excluded. Directory
revisions are revalidated; membership/create/edit/delete/catch-up decisions share
the conversation's PostgreSQL advisory lock. Catch-up also compares a hash of
the current envelope to reject stale work after an edit. The initial accepted
hash remains available to acknowledge a lost-response create retry.

Catch-up requires a sender device that can decrypt the message to reconnect
after enrollment. The recipient need not be online simultaneously. The relay
cannot complete this itself and does not hold private keys. Catch-up is an
authorized sender operation; as with encrypted edits, the relay cannot inspect
or verify equality of the plaintext inside replacement ciphertext.

`encryptAttachment(scope,context,source,directories)` returns `{stream,key}`.
OPFS writes must snapshot each typed-array chunk as `new Blob([chunk])`, including
the decrypted quarantine sink and final cache copy. WebKit's
[file sink](https://github.com/WebKit/WebKit/blob/main/Source/WebCore/Modules/filesystem/FileSystemWritableFileStreamSink.cpp)
can use the entire backing buffer of an ArrayBufferView. OpenPGP emits views
smaller than their buffers; writing those directly inserts extra bytes and
corrupts packet framing. Keep stream backpressure rather than buffering the full
attachment. `encrypted-files.test.mjs` models that sink behavior and verifies a
real encrypted upload/download round trip for sender and recipient.

The AES-256 session key (`{algorithm:'aes256',data:<64 hex digits>}`) is stored
**only inside the signed encrypted message attachment descriptor**, with the
original file's sender device and directory revision. Never include this key in
upload metadata, HTTP logs, the API's plaintext file model or server storage.
`decryptStream(...,sink,attachmentKey)` can use that authenticated descriptor key
to open the original file for a late recipient. This bypasses only the file
header's original recipient-list check: the original device signature, accepted
revision, exact message/file context, full-stream integrity and quarantine commit
remain mandatory. No attachment re-upload is needed for catch-up. Ordinary
message and call-control decryption never accepts an attachment key.

Encrypted attachment objects use `application/octet-stream` and an opaque
`attachment.pgp` filename. Voice recordings instead use `voice.pgp` so the server
can enforce the conversation's voice-message switch independently of its file
switch. This exposes **voice-versus-other-attachment category**, alongside routing,
membership, timing and sizes. Original names, MIME types and contents remain
encrypted. Profile/group avatar images use the separate authorized image feature;
they are not encrypted chat attachments.

`device.js` stages streamed ciphertext in OPFS before chunked upload, with at most
4 GiB plaintext plus 16 MiB ciphertext overhead. Decrypted OPFS files remain
quarantined until the stream API verifies the complete signature and context.
After closing the final file, a ready sidecar records its account, signed-context
digest and byte size. Cache existence alone is insufficient. A valid sidecar lets
the client open verified media offline without fetching a sender directory;
startup removes abandoned temporary files. The sidecar protects against partial
writes and cache mix-ups, not a compromised same-origin script.

For call-key envelopes include call ID, epoch, recipient device and authoritative
roster binding in context and use exact-device encryption. SFrame itself and
epoch agreement are separate; this module does not implement an audio cipher.
The [Bolt SFrame adapter](../../Libraries/Bolt/Bolt.Media.Browser/sframe/README.md)
uses pinned sframe 2.0.0 WASM and ring 0.17.14. Encrypted calls require the explicit
authenticated SFrame mode, exact-device key envelopes, immediate pause on roster
change and acknowledgment by all current members before a fresh epoch transmits.
There is no silent trusted-server fallback. The prepared group path is bounded
to eight participants with a 128 kbps Opus encoder target; WebCodecs and the
managed Concentus fallback share the audio lifecycle. Legacy experimental ECDH
remains unavailable. The desktop fixture has demonstrated three-browser SFrame
exchange at the configured 128 kbps target, mute/unmute, departure and recovery
revocation with a continuing two-party epoch. Public deployment and physical
mobile validation remain separate from these client/gateway proofs.

## Notification previews

A push payload still carries routing identifiers only. The sender and the message
text on a banner are produced on the device: `service-worker.module.js` is a
module service worker that statically imports `encryption.mjs`, so a push can read
`GET /api/chat/conversations` with the same-origin cookie, fetch the sender
directory and decrypt the conversation's last envelope through the ordinary
`decrypt` path - the same directory validation, pinning, rollback and revocation
checks. There is no second decrypt implementation, and no plaintext is written
anywhere: it exists for one `showNotification` call. The only storage write is the
trust pin `validateDirectory` already makes, under the same `yap-encryption` lock
the app uses.

One banner per push, and only one. The push handler resolves a descriptor first -
the decrypted message if it arrives inside a 2 s budget, the generic "New message"
otherwise - and calls `showNotification` exactly once, as its last statement,
under a `try` that falls back to a fixed generic descriptor if anything above it
throws. Every failing outcome - no encryption identity on this device, an envelope
it cannot open, offline, a slow network, a push for an account this device is not
signed into, or the Settings toggle off - lands on that same single call, which is
what `userVisibleOnly` requires. This replaced a show-then-replace ordering:
iOS treats a second `showNotification` with an existing tag as a new notification
rather than a replacement, so every message arrived as two stacked banners. The
worst case before anything appears is now the budget.

Each message gets its own banner, tagged `yap-msg-<notificationId>` from the inbox
item id the payload already carries, so three messages are three notifications
instead of one that keeps being overwritten. The id is fixed when the delivery job
is queued, so a redelivery of the same push replaces its own banner. Calls still
collapse on `yap-call-<reference>`; only a payload with no message id falls back to
collapsing per conversation.

`PushEnvelope.Account` ("tenantId:credentialId") is what makes multi-account
devices work: it selects the IndexedDB key scope and the `X-Yap-Account` header.
It is stamped server-side from the credential the push is addressed to, carries
nothing the recipient does not already know about themselves, and the BFF still
requires the header to match the signed-in cookie, so a push for the other account
fails closed with a 401.

Where message text may appear is a per-device, per-account setting stored in
`yap-notifications-v1` (IndexedDB, because a service worker has no localStorage),
default on. Turning it off keeps the generic banner and issues no request at all.

Limitations. Only browsers with module service workers - Chrome 91+, Safari 16.4+,
Firefox 147+ - get previews; older browsers fall back to the classic worker and
today's generic banner, and no iOS release that can receive a web push at all is
below that line. In a group the banner titles the conversation, not the individual
sender, because the conversation row carries no per-message sender name. iOS's own
"Show Previews" notification setting can hide banner content until the phone is
unlocked no matter what the app renders; that is an OS-level user preference, not
a defect in this path. Loading the OpenPGP bundle at worker startup costs roughly
20 ms on a desktop and plausibly 60-150 ms on a mid-range phone, on every module
worker start including the cold navigations that serve the offline shell.

## Security and verification limits

First contact is TOFU until fingerprints are compared. A fresh device cannot
detect a malicious server's first-contact substitution without that comparison.
Persisted pins detect subsequent changes and roster rollback. Server routing
metadata and traffic timing remain visible. Origin script compromise/XSS can
read unlocked local keys. OpenPGP static device keys do not provide forward
secrecy or post-compromise security; do not make those claims for messages or
call-key envelopes.
In particular, later private-key compromise can decrypt recorded OpenPGP
call-key envelopes and therefore expose recorded calls; adding SFrame does not
make that key distribution forward-secret. A stolen master/recovery secret is
account-authority compromise, beyond ordinary device revocation. Older plaintext
messages do not become confidential retroactively. Once password recovery is on,
the account password *is* sufficient to restore encrypted history on a new
device: the OPAQUE export key never leaves the browser, but anyone who knows the
password obtains it and can unwrap the recovery secret. Message history is
therefore only as confidential as that password; the recovery key remains the
fallback for a password its owner has forgotten, and a server-side password
reset deletes the OPAQUE credential and its wrapped envelope, so the new
password alone cannot restore history.

`password-unlock.test.mjs` additionally covers a sign-out and sign-in on an
account that already has an identity: the published root is untouched, the peer
that had pinned it sees no security-key change, and a device that cannot recover
reports the lock instead of enrolling over the account.

Run `node --test src/Presentation/XFramework.Yap.Client/test/encryption.test.mjs`.
Tests use the actual vendored browser crypto bundle, with in-memory storage.
They cover signed multi-recipient exchange, wrong signatures, unsigned/tampered
content, context replay, directory substitution/rollback, account isolation,
stable recovery and fresh identities, revoked devices, exact-device call keys,
stream integrity/quarantine and an 80 MiB stream with bounded output chunks.
These tests are not an external security audit or a real iOS/Android UI test.
Also run `node --test src/Presentation/XFramework.Yap.Client/test/encrypted-files.test.mjs`
for OPFS verification markers, interrupted writes, offline reads and account
isolation.

Completed desktop Chrome integration proofs include encrypted exchange among
three accounts, a 7 MB JPEG received by all participants and opened in the viewer,
MOV playback, additional-device approval with owner rotation, and recovery that
revokes former devices. Actual browser encrypted upload/download of an 80 MiB
(83,886,080-byte) file produced matching SHA-256 hashes:
`3A947FE28882D03D4D8948E94D4A870BF4ACB1809B4D9D78090CDBF323FD1AAC`.
The 4 GiB limit has not thereby been stress-tested.

The lasting [browser verification report](../../../docs/solutions/architecture-patterns/yap-encryption-browser-verification.md)
records actual three-party SFrame frame counts, 128,000 bits/s encoder settings,
nonzero decoded audio and old-device removal after recovery. Both retained peers
activated a new two-party epoch and continued audio while the revoked device's
counters stayed fixed. The same report records mute/unmute and departure behavior.

Encrypted edits now also passed across all three accounts. A deliberate
three-second file-link delay displayed the recipient skeleton first and then
automatically showed the preview on all three clients. Raw server content stayed
generic plus the armored envelope. The final focused regression run passed 173
tests: 49 client .NET, 103 host .NET and 21 JavaScript.

Public WSS deployment, final release-build
configuration and physical iOS/Android testing also remain gates. A mobile
viewport in desktop Chrome is not a physical Safari or Android test, and these
fixture runs are not an independent security audit.

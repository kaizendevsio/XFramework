# Browser encryption integration

`wwwroot/encryption.mjs` exports `encryption` and installs the same object at
`window.yap.encryption`. Import it as a module. The vendored OpenPGP.js 6.3.1
bundle is unmodified; its npm SHA-512 integrity was checked before copying.
It uses RFC 9580 authenticated encryption and signatures, with 256 KiB AEAD
chunks. No plaintext fallback exists in this library.

## Account/device lifecycle

Scope is `{tenantId, credentialId}` (GUID D strings), or `tenantN:credentialN`.
All persistence and trust pins are bound to this account and tenant.

- `status(scope)` returns `enrolled`, `approved`, `deviceId`, `rootFingerprint`,
  `directoryRevision`, `canApproveDevices`, and `verifiedContacts`.
- `initialize(scope)` persists a pending identity before returning
  `{directory,recoveryArchive,recoveryKey}`. Repeated calls retain the identity.
  Publish directory with expected revision 0, then call `acceptDirectory`.
- `acceptDirectory(scope,directory)` validates the full signed roster, approval
  and revocation records. It pins the root fingerprint, highest revision and
  manifest digest. Root changes, rollback and conflicting same-revision records
  fail. Own device approval becomes active only after accepting its directory.
- `verifyFingerprint(scope,credentialId,fingerprint)` marks an existing matching
  pin verified. The user must compare fingerprints through another trusted path.
- `proposeDevice(scope)` creates a fresh keypair and returns a public proposal.
- `approveDevice(scope,proposal,directory)` returns
  `{expectedRevision,directory,approval}`. Publish its directory using CAS, then
  give the approval object to the new device through the explicit approval flow.
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
For complete history transfer to an additional device, re-encrypt cached history
content to its fresh key; never copy the private key of another active device.

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

For call-key envelopes include call ID, epoch, recipient device and authoritative
roster binding in context and use exact-device encryption. SFrame itself and
epoch agreement are separate; this module does not implement an audio cipher.

## Security and verification limits

First contact is TOFU until fingerprints are compared. A fresh device cannot
detect a malicious server's first-contact substitution without that comparison.
Persisted pins detect subsequent changes and roster rollback. Server routing
metadata and traffic timing remain visible. Origin script compromise/XSS can
read unlocked local keys. OpenPGP static device keys do not provide forward
secrecy or post-compromise security; do not make those claims for messages or
call-key envelopes.

Run `node --test src/Presentation/XFramework.Yap.Client/test/encryption.test.mjs`.
Tests use the actual vendored browser crypto bundle, with in-memory storage.
They cover signed multi-recipient exchange, wrong signatures, unsigned/tampered
content, context replay, directory substitution/rollback, account isolation,
stable recovery and fresh identities, revoked devices, exact-device call keys,
stream integrity/quarantine and an 80 MiB stream with bounded output chunks.
These tests are not an external security audit or a real iOS/Android UI test.

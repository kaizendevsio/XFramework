---
title: "feat: Single-password Yap encryption recovery with OPAQUE"
type: feat
status: active
date: 2026-09-16
---

# Objective

Sign in to Yap with one password and automatically restore the existing encryption identity and historical decryption keys. Keep plaintext passwords and backup-unlocking keys out of the server during OPAQUE authentication. A genuine identity reset must have an explicit contact-verification path rather than indefinitely blocked chats.

# Design

- IdentityServer owns OPAQUE credentials, authentication, credential migration and password lifecycle in its existing database schema. Yap owns its browser session and client-side backup wrapping. No external identity provider.
- Use RFC 9807 OPAQUE through a pinned maintained implementation. Use `opaque-ke` 4.0.1 for the server and `@serenity-kit/opaque` 1.1.0 for the compatible WASM client, with the memory-constrained Argon2 profile (64 MiB, three iterations, four lanes). Record exact versions, licenses, audit scope and interoperability evidence. Existing audits do not certify our integration or every later library release.
- Preserve random message/account keys. Derive a separate backup wrapping key from the client-only OPAQUE export key with application/account context; never use the shared session key or server password verifier. Authenticated encryption protects the backup and binds its account/version.
- Registration and login use bounded multi-step exchanges, tenant/account/context binding, short-lived single-use server state, generic authentication failures and existing rate limits/account/role checks. Issue normal sessions only after successful final verification.
- Store the OPAQUE server setup securely and persist it across deployments; losing it prevents password recovery. Do not log setup, exchange state, export keys, passwords or backup plaintext.
- Migrate existing users only with authenticated password proof and accessible encryption keys. Publish and verify the wrapped backup before retiring the legacy password path. Do not manufacture a new identity when backup restore fails.
- An existing local identity or recovery key can enable migration. Missing keys cannot be reconstructed; clearly distinguish account login from unavailable historical encryption keys.
- Password changes must atomically replace credentials and the wrapped recovery secret while preserving message keys. Password reset without any surviving recovery method cannot recover old history. Revocation/session rules remain enforced.
- Logout clears active sensitive state; returning users restore from their password-protected backup. Local offline use remains available according to existing account/cache policy.
- Identity reset retains explicit contact key-change verification. Never silently trust server-supplied replacement identity keys or silently discard blocked messages.

# Execution checklist

- [x] Inspect current password authentication, encrypted recovery archives and identity pinning.
- [x] Select and validate browser/server OPAQUE library interoperability, failure handling and build packaging.
- [x] Add IdentityServer persistence, endpoints, authentication integration and migration.
- [x] Add browser OPAQUE authentication and automatic encrypted backup protection/restoration.
- [x] Handle password change/reset, logout, interrupted enrollment and lost-key states.
- [x] Expose actionable identity-change verification so existing chats can resume after reset.
- [ ] Run protocol, authorization, replay, concurrency, migration, recovery and browser regression tests.
- [x] Document operational secrets/rollback limits and release notes.
- [ ] Merge and deploy; verify IdentityServer/Yap health, exact release assets and restore on a fresh browser profile.

# Acceptance evidence

Test correct/wrong password, malformed/replayed/cross-account exchanges, unknown/disabled accounts, missing roles, stale credential epochs and concurrent enrollment. Test migration preserving old message/attachment keys, login on a fresh device, interrupted backup upload, logout/login, password change, forgotten password with/without surviving device, and contact acceptance after real identity reset. Assert plaintext passwords and export keys never appear in OPAQUE network requests or server logs. Verify mobile-compatible worker execution and no false promise of permanent recovery.

# Security boundaries

OPAQUE does not eliminate password guessing after full server compromise, malicious code delivered by a compromised web origin, or loss of all recovery factors. Strong passwords, rate limits, protected server setup, dependency review and normal HTTPS remain required. Existing Yap message encryption is not changed into a forward-secret ratchet by this work.

# Rollout

Keep legacy users functional while migration is incomplete. Do not enable a partially implemented recovery path in production. Deployment must include IdentityServer and Yap together and preserve the OPAQUE setup and database. Rollback after migration must not re-enable legacy password authentication for migrated accounts or discard encrypted backups.

# References

- https://www.rfc-editor.org/rfc/rfc9807.html
- https://github.com/facebook/opaque-ke
- https://github.com/serenity-kit/opaque
- https://www.opentech.fund/security-safety-audits/opaque-security-audit/

# Implementation and validation record

Release: Yap 1.3.39. Thin Rust native adapter invokes the pinned OPAQUE implementation; the browser uses the matching unmodified WASM package in the existing encryption worker. Rust source and Cargo.lock are committed. The Docker build pins Rust 1.88.0 by digest and copies only the resulting native library into IdentityServer.

Validated locally:
- 138 IdentityServer PostgreSQL/Bolt integration tests passed, including real browser/native registration, migration, password change, session revocation, proof replay, disabled accounts, and atomic OPAQUE-backed identity reset.
- 103 IdentityServer unit tests passed, including exchange tenant/role/actor/expiry binding.
- 172 Yap server tests and 161 Yap client tests passed.
- 38 JavaScript tests passed, including native/WASM interoperability, wrong passwords, malformed/tampered exchanges, encrypted wrapping scope binding, fresh-device history restoration, preservation of existing devices, backup merging, and lost committed-response recovery.
- Browser fixture sign-in succeeded through the real worker/module loading path. Live fresh-profile verification remains a rollout gate.

The existing recovery persistence suite exercises stale revisions/concurrent directory and archive updates. Enrollment captures both the credential stamp/OPAQUE epoch and the backup root/revision; its transaction rejects an intervening change. This is integration coverage, not an independent cryptographic audit or a physical iOS/Android certification.

# Operations and compatibility

- `Opaque:SetupPath` / `Opaque__SetupPath` points to `/var/lib/xframework/identity/opaque-setup-v1` on the existing `identity-keydata` volume. Provision once from `xfw_opaque_execute({operation:"setup"})`, writing the returned base64url setup directly into a mode-0600 file. Never print it, commit it, send it to a browser, or regenerate it on restart. Missing/invalid setup fails IdentityServer readiness.
- On xeon-dev the setup was provisioned before rollout, with mode 0600 and 172 bytes including newline. Its contents were not logged. Include this volume in protected, access-controlled backups alongside PostgreSQL. Restore the same setup with its credential database; restoring only the database is insufficient.
- Multi-step exchanges are in process, bounded to 256 and expire after two minutes. Restarting IdentityServer invalidates unfinished exchanges; users retry. More than one IdentityServer replica requires a shared protected exchange store or sticky routing before scale-out.
- This release enables OPAQUE through the Yap trusted-service boundary. Existing accounts migrate only after password verification and a verified encrypted backup. Old Yap builds must update before signing into a migrated account. Other tenants' legacy accounts continue unchanged.
- A migrated credential cannot use legacy password sign-in from another client. A read-only preflight found 19 active Yap-tenant credentials and zero with another role. Any future shared account must use an OPAQUE-capable client; do not restore a BCrypt fallback for compatibility.
- Password change rewraps the same random recovery secret and revokes old sessions atomically. Existing account-reset-token/SMS-verification flows restore account access, remove the old OPAQUE credential, and retain the encrypted archive. The new password alone cannot decrypt that archive: use a surviving device/recovery key and re-enable password recovery, or explicitly reset the encryption identity.
- Actual identity reset atomically replaces the public directory, encrypted archive and wrapped recovery secret. Peers still need to compare/accept the new fingerprint. Pending sends pause with instructions; accepting the fingerprint retries the queue. No automatic trust downgrade.
- Once any credential migrates, rollback must retain an OPAQUE-capable IdentityServer/Yap pair and the new table/setup. Do not deploy an old password-only UI/server, reverse the migration, or replace the setup. Prefer a forward fix. Deployment health gates must pass before live migration verification.

# Remaining rollout evidence

Record the merged commit, deployment run, public readiness/version checks and fresh-profile recovery result here after rollout. Do not mark deployment complete from a successful build alone.

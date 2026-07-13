---
title: "Bolt Phase 1 Centralized Asymmetric Transport Identity"
date: 2026-07-13
category: architecture-patterns
module: Bolt
problem_type: architecture
component: transport
severity: critical
applies_when:
  - "Designing or reviewing Bolt Phase 1 transport authentication"
  - "Changing Bolt transport-token issuance, validation, renewal, rotation, or revocation"
tags: [bolt, identityserver, transport-identity, jwks, revocation, security]
status: historical
---

# Bolt Phase 1 Centralized Asymmetric Transport Identity

**Decision status:** Draft, research-only, and non-authoritative. The frontmatter uses `historical`
because the solution knowledgebase reserves that status for context-only material; it does not mean
this proposal was implemented or accepted.

**Authority:** This document records constraints and candidate decisions for review. It is not an
implementation specification, rollout approval, or permission to change production authentication.

**Implementation gate:** Phase 0 must be deployed, observed, and marked `Verified` under the
[Phase 0 deployment gate](../workflow-issues/bolt-phase0-deployment-gate-2026-07-13.md).
No Phase 1 production code, merge, or rollout may begin before that gate passes.

## Context

Phase 0 does not yet centralize normal Bolt client transport-token acquisition. Its protected
synthetics acquire tokens from IdentityServer over HTTPS, while regular service clients still mint
Hub-accepted tokens locally with the shared application HMAC key. Any caller that possesses that
key can mint a token. Hub verification therefore is not yet a production service trust boundary.

The current shape also has lifecycle gaps that Phase 1 must close:

- services and the Hub share verification material accepted for transport authentication;
- IdentityServer currently depends on a healthy Hub during Compose startup;
- the Hub has no JWKS lifecycle or active revocation feed;
- connections are not indexed by signing key, token ID, and principal for bounded revocation;
- the client token-provider contract returns only a bearer string and cannot schedule
  expiry-aware, jittered, single-flight renewal;
- existing asymmetric RPC signing keys store private PEM material in the shared database and are
  not an acceptable transport-signing root.

## Proposed Decision

After the migration gate completes, Bolt Hub will accept only short-lived asymmetric transport
tickets issued by IdentityServer.
No caller, service workload, shared database credential, or Hub process will possess a private
key capable of signing a Hub-accepted transport ticket.

**ES256 and RS256 remain candidates.** Exactly one algorithm must be selected before the first
implementation PR, based on production signer compatibility and a Phase 0 production-equivalent
CPU, allocation, throughput, and p99 benchmark. RS256 is the compatibility default until that
selection is approved. The Hub, issuer, JWKS manifest, and revocation verifier must then allow only
the selected algorithm; runtime negotiation or a multi-algorithm fallback is prohibited.

This decision covers WebSocket connection authentication and server-derived transport identity.
It does not change Bolt v1 framing, RPC actor/service tokens, topic capabilities, Media, or
multi-Hub routing.

## Trust Boundaries

| Boundary | Required trust |
|---|---|
| IdentityServer identity plane | Authenticates workloads and users over HTTPS, authorizes transport-ticket issuance, publishes JWKS and signed revocation status, and starts without Bolt. |
| Transport signer | IdentityServer-only, preferably a non-exportable KMS/HSM key. An IdentityServer-only encrypted file/secret-store key is the minimum development fallback. Private material in the shared database is prohibited. |
| Bolt Hub | Holds Hub TLS private material plus transport and revocation public keys only. It validates tickets, derives identity, maintains bounded security indexes, and closes matching sockets. |
| Service workload | Holds its own bootstrap credential or mTLS private key. It never receives transport signing or Hub-verification private material. |
| User/browser | Exchanges an IdentityServer-exclusive, live server-side HTTPS session for a user transport ticket. A shared-HMAC claim is never session proof. Query carriage is browser-only and must be redacted before logging or telemetry. |
| Revocation authority | Uses a signing key distinct from transport issuance and TLS keys. It publishes monotonic, bounded status state for principals, token IDs, and compromised transport keys. |
| Trust-manifest authority | Uses a dedicated non-exportable signing key whose public key is provisioned with the Hub. It signs monotonic manifests that bind issuer origins, allowed algorithms, and transport/revocation public keys. |
| TLS authorities | Hub TLS, IdentityServer TLS, workload mTLS, transport signing, and revocation signing keys remain cryptographically and operationally distinct. |

## Ticket Contract

Transport tickets use a dedicated issuer and Bolt-Hub-only audience. Header and payload parsing
must reject duplicate JSON properties before cryptographic validation.

Required JOSE header fields:

- `typ=bolt-transport+jwt`;
- `alg` equal to the one Phase 1 selected algorithm;
- a nonempty, allowlisted `kid`.

The claim contract is an allowlist, not a minimum list:

| Principal | Required claims | Optional claims | Forbidden aliases and authority |
|---|---|---|---|
| Service | `iss`, `aud`, `scope`, `sub`, `principal_type`, `service_name`, `iat`, `nbf`, `exp`, `jti` | None | `service`, `serviceId`, `client_id`, `clientId`, route IDs, user/tenant/role/permission claims, and every claim not explicitly required |
| User | `iss`, `aud`, `scope`, `sub`, `principal_type`, `tenant_id`, `credential_id`, `session_id`, `iat`, `nbf`, `exp`, `jti` | At most one `device_id` | service/route claims, username/email/display-name, roles, permissions, groups, alternate tenant/credential/session aliases, and every claim not explicitly required or optional |

Each required claim occurs exactly once. `aud` is exactly one scalar Bolt Hub audience,
`scope` is exactly the scalar `bolt.transport.connect`, and `principal_type` is exactly `service`
or `user` for its corresponding allowlist. Arrays, duplicate properties, multiple audiences,
space-delimited scopes, empty strings, type coercion, case folding, and Unicode normalization are
rejected. JOSE headers other than `typ`, `alg`, and `kid`, including `jku`, `x5u`, embedded keys,
and unrecognized `crit` values, are rejected.

The maximum lifetime is 60 seconds and accepted clock skew is at most 5 seconds. A ticket with
missing, conflicting, duplicate, unexpected, or ambiguous authority claims fails closed.

The following parsing ceilings are provisional and must be replaced or confirmed by the required
Phase 0 baseline before implementation: compact ticket length 4,096 bytes, at most 16 payload
properties, at most three JOSE header properties, at most 256 UTF-8 bytes per string value, and at
most 128 UTF-8 bytes for `service_name`. The parser checks encoded and decoded lengths before
allocation and rejects values above the approved ceilings.

For service tickets, `service_name` is the exact current Bolt v1 case-sensitive UTF-8 service name;
there is no trimming, case conversion, aliasing, or normalization. The existing client ID remains
the lowercase hexadecimal SHA-256 of those exact UTF-8 bytes. The existing FNV-1a 32-bit routing
value and v1 registration/framing behavior remain unchanged and are never treated as proof of
identity. Client-supplied registration ID/name/hash fields are compatibility metadata only and
must exactly match the server-derived values.

## Issuance And Bootstrap

1. A service obtains a ticket from IdentityServer over HTTPS. The production workload-bootstrap
   mechanism is unresolved: workload mTLS and per-workload, exact-service client credentials are
   the candidates. Neither candidate may sign transport tickets or travel through Bolt.
2. A user exchange requires an IdentityServer-exclusive opaque session credential that resolves
   to a live, server-side session record at issuance time. IdentityServer must verify that the
   session is enabled, unexpired, unrevoked, bound to the requested credential/tenant, and protected
   by the approved cookie origin, SameSite, Secure, and anti-CSRF controls. A JWT, bearer token, or
   claim set verifiable with the shared application HMAC key is never accepted as proof of that
   session, even when its claims appear valid.
3. IdentityServer issues its own service ticket through the same local signer abstraction without
   a Bolt call or HTTP self-call.
4. IdentityServer HTTP/JWKS/readiness starts independently of Bolt. Its optional Bolt client
   registration follows Hub availability and does not gate identity-plane readiness.
5. Service clients send the ticket in the WebSocket `Authorization` header. Browser clients may
   use the fully redacted query-token path only where browser APIs cannot set that header.

No issuance failure permits fallback to locally minted HMAC transport credentials.

The production workload-bootstrap decision must have an owner and fixed UTC decision deadline in
the Phase 1 rollout manifest. That deadline is no later than 14 calendar days before the first
implementation PR and must precede ADR approval. Missing the deadline blocks implementation; it
does not silently select client secrets or mTLS.

## Client Renewal

Add `IBoltTransportTokenProvider` separately from RPC `IServiceTokenProvider`. Its result includes
the bearer value, `kid`, `jti`, issued-at time, not-before time, and expiry without exposing those
fields through logs.

Clients renew with per-identity single-flight acquisition and bounded jitter before expiry.
Phase 1 uses make-before-break connection replacement rather than an unversioned in-band
reauthentication frame. For each bounded pool batch, the client acquires a fresh ticket, opens WSS,
and completes authenticated registration on the replacement before the old connection enters
drain. Pool admission reserves explicit temporary headroom for one replacement per draining
connection, while a configured batch ceiling prevents a full-pool reconnect surge from doubling
the pool at once. If headroom is unavailable, renewal starts earlier or fails closed; it never
evicts unrelated connections or exceeds the memory/connection quota.

A draining connection accepts no new RPC, stream, or subscription work. Existing unary work may
finish only before the earliest of ticket expiry and the approved drain timeout. Existing streams
are not migrated: at that deadline they receive the protocol's defined transport-cancelled result,
the socket closes with the versioned renewal/drain close reason, and retry remains an explicit
caller policy. The implementation specification must pin that result and close reason before code
is written. No stream or socket survives ticket expiry.

Concurrent pool expansion shares the same valid acquisition result where policy permits. Failed
acquisitions are not cached beyond a bounded retry backoff, and every waiting caller remains
cancellable and deadline-bound.

## JWKS Lifecycle

The Hub's initial trust is not derived from the first JWKS response. Its deployment contains:

- a dedicated IdentityServer HTTPS CA or equivalent pinned origin trust anchor;
- the exact HTTPS origin and path allowed to serve the manifest and keys;
- the trust-manifest authority public key and its one allowed algorithm;
- a minimum accepted manifest version.

IdentityServer publishes a canonical, signed, versioned key manifest that binds the issuer, exact
HTTPS origin, selected JWT algorithm, active/not-before/retirement state, and transport and
revocation public-key fingerprints. The Hub verifies TLS first and the manifest signature second.
It atomically persists the highest accepted manifest version and key-state epoch outside the
container writable layer. That durable high-water survives restart, redeploy, and temporary loss
of IdentityServer. A lower version/epoch, reused version with different bytes, unlisted key, or
origin/algorithm substitution is a rollback and fails closed.

- Publish a new validation key at least 120 seconds before issuance activation.
- Use a dedicated Bolt JWKS/manifest poller at least every 15 seconds, with bounded jitter,
  single-flight work, response-size limits, and a bounded 30-second maximum freshness for new
  authentication decisions. The stock token `ConfigurationManager` refresh path is not sufficient
  because its minimum refresh behavior cannot satisfy this 15-second contract.
- Refresh an unknown `kid` once through the same rate-limited poller. Unknown or removed keys
  remain rejected if refresh fails.
- Retain old public keys for at least twice the maximum ticket lifetime and until all tickets plus
  clock skew have expired.
- Scheduled rollover, emergency key compromise, and key removal are explicit state transitions
  in the signed manifest with monotonic evidence.
- Invalid JSON/signatures, duplicate keys/properties, algorithm substitution, oversized content,
  stale state, rollback, or an untrusted TLS response fails new authentication closed.

IdentityServer outage does not enable HMAC fallback. Existing sockets remain bounded by their
ticket expiry; new issuance and any authentication requiring stale or unknown key state fail.

## Revocation Lifecycle

IdentityServer publishes a compact signed status JWT over HTTPS using the separate revocation key.
The private key is IdentityServer-only and non-exportable in the production KMS/HSM; the encrypted
IdentityServer-only development fallback is never a production rollout option. It uses the one
algorithm selected for Phase 1 but a `kid` and key purpose disjoint from transport-ticket issuance.
The proposed hard status lifetime is 30 seconds with at most 5 seconds of accepted clock skew:
`exp - iat` must not exceed 30 seconds, and a status is not accepted when its issue time is more than
30 seconds old at validation. Hubs poll at least every 5 seconds, give each poll a hard 3-second
deadline, and must obtain a valid status reflecting a published epoch within 15 seconds. These
values remain subject to the Phase 0 baseline, but implementation must replace them with equally
explicit approved bounds rather than an unbounded or configuration-only default.
The trust manifest prepublishes a replacement revocation key, records activation and retirement,
and provides an overlap at least as long as the maximum status lifetime plus skew. Compromise
increments the signed manifest state and revocation epoch; it never reuses a `kid` or epoch.

The status contains `typ=bolt-revocation+jwt`, issuer, Hub audience, issue/expiry times, manifest
version, a strictly monotonic epoch, and bounded entries for:

- disabled users and services;
- unexpired revoked `jti` values;
- compromised transport-signing `kid` values.

The Hub atomically persists the highest valid revocation epoch and corresponding content digest
outside the container writable layer. A lower epoch or same epoch with different content is a
rollback across both live operation and restart.

These ceilings are provisional until confirmed or replaced by the Phase 0 baseline: 1 MiB encoded
response, 4 MiB decoded content, 50,000 entries total, and 256 UTF-8 bytes per identifier. The
connection security index permits exactly one record per accepted live connection, no more than
500,000 records, a measured 512-byte per-record allocation budget, and 256 MiB aggregate memory.
The principal, `jti`, and `kid` lookup structures share that aggregate budget and cannot each claim
it independently. Hitting any item, byte, record, or memory ceiling fails new authentication closed,
emits bounded diagnostics, and never drops active revocations to make room.

Entries are pruned after their last possible ticket expiry plus skew. Oversized, duplicate, stale,
future-dated, rolled-back, incorrectly signed, or expired status fails new authentication closed.
Once the last accepted status exceeds either its signed expiry or the 30-second maximum accepted
age, the Hub immediately rejects new registration and renewal. It also stops admitting frames on
existing sockets and closes every existing socket within 5 seconds of freshness expiry; ticket
expiry is not allowed to extend a socket whose revocation state is stale. Recovery requires a new
valid status at or above the durable epoch high-water and never falls back to cached HMAC authority.

Hub polling observes a valid new epoch within 15 seconds. Push delivery is optional and always has
poll fallback. Each connection records `kid`, `jti`, principal/service identity, issue time, and
expiry in bounded indexes. A matching revocation closes all indexed sockets within 60 seconds.
Index insertion, removal, reconnect, socket close, and status replacement have deterministic
ownership and bounded cleanup.

## User And Service Separation

The Hub uses distinct service and user authentication schemes and authorization policies. A user
ticket cannot register a reserved service route, join a service pool, subscribe to service-only
topics, or receive service RPC. A service ticket cannot silently acquire user/tenant authority.
Ambiguous tokens that satisfy neither exact scheme, or appear to satisfy both, are rejected.

During the bounded migration only, the asymmetric and legacy HMAC schemes have disjoint
`typ`, `iss`, `aud`, algorithm, and key sets. Dispatch selects a scheme from those unverified
structural fields only to choose the verifier; those fields grant no authority before verification.
Once a token is dispatched to the asymmetric verifier, any parse, signature, key, claim, freshness,
or revocation failure is final. The Hub must not retry it as HMAC. A token matching both or neither
dispatch contract is rejected before either authorization policy runs.

## Compatibility And Rollout

Bolt v1 frames and registration messages remain wire-compatible. Rollout order is:

1. **Baseline gate:** record a Phase 0 production-equivalent transport baseline and approve the
   exact algorithm, thresholds, bootstrap decision, manifest version, release SHA, and rollback.
2. **Trust gate:** make IdentityServer HTTP/JWKS readiness independent of Bolt, provision pinned
   trust anchors, and prepublish the first transport and revocation keys.
3. **Shadow gate:** deploy Hub manifest/JWKS/revocation validation and dispatch metrics without
   accepting identity mismatch or changing authority.
4. **Canary gate:** migrate IdentityServer and the named canary workload cohort; pass security,
   correctness, p99, CPU, allocation, throughput, reconnect, and memory-index limits.
5. **Cohort gate:** migrate the explicitly versioned remaining-service and user/browser cohorts;
   each cohort has independent go/no-go evidence and no expansion after a failed gate.
6. **Asymmetric-only gate:** force expiry or closure of every legacy socket, remove the HMAC
   transport verifier and local minting, rotate/revoke the old shared HMAC secret, and prove
   rejection on every Hub.
7. **Phase 1 security gate:** verify no HMAC-accepted socket, code path, key, or rollback target
   remains. Phase 1 cannot be declared secure, complete, or generally available before this gate.

Before rollout, an immutable signed rollout manifest records a unique rollout ID, UTC start,
UTC HMAC-disable deadline, build/commit SHA, ticket and manifest schema versions, exact cohort
membership, gate owners, metric thresholds, and rollback target. The proposed dual-scheme maximum
is seven calendar days and never more than one release train; the approved manifest must contain
the exact UTC deadline. Permanent dual authentication, extending the deadline without a new
security review, legacy identity mismatch, caller-held signing authority, and rollback to locally
minted Hub credentials are prohibited.

Rollback may return to the most recent security-qualified asymmetric configuration only. If none
exists, keep Bolt unavailable while preserving safe HTTP identity surfaces.

## Performance Evidence Gate

Phase 1 cannot depend on a future Phase 2 benchmark. Before implementation starts, Phase 0 must
produce a production-equivalent baseline on pinned hardware, OS/runtime, TLS, payload distributions,
connection counts, pool sizes, and the enabled unary and streaming mixes. Media is disabled in
Phase 0 and is excluded from this baseline; its benchmarks belong to the later Media gates. Raw
BenchmarkDotNet and sustained-load
artifacts must record throughput, p50/p95/p99, CPU time, allocated bytes, Gen0/1/2 collections,
working set, reconnect rate, ticket issuance latency, Hub handshake latency, and security-index
bytes per connection.

ES256 and RS256 must be measured with the intended production signer and the exact .NET verifier.
Exactly one is selected before implementation; RS256 remains the compatibility default while that
decision is open. The current early guard of no more than 5 percent regression in p99, throughput,
CPU, or allocated bytes is a proposal, not an accepted threshold. Ticket/revocation size ceilings,
renewal timing and batch size, pool headroom, index memory limits, and every performance threshold
in this ADR are likewise proposals until the baseline records approved values and variance bands.
No average can waive a p99, allocation, working-set, reconnect-storm, or fail-closed regression.

## Rejected Alternatives

| Alternative | Reason rejected |
|---|---|
| Keep the shared HMAC and centralize only its API | Callers still possess Hub-accepted signing authority. |
| Treat a valid shared-HMAC application token as user-session proof | Any holder of the shared key could forge the claims and use IdentityServer as an asymmetric signing oracle. |
| Reuse existing database-backed service RPC signing keys | Private PEM material is stored in the shared database and has the wrong ownership and lifecycle. |
| Use mTLS as the only transport identity | Appropriate for workloads but does not cover browser/user identity or application-level revocation semantics. |
| Introspect every connection synchronously | Makes Hub authentication availability and latency depend on a per-connection IdentityServer request and complicates outage behavior. |
| Add an unversioned in-band reauthentication frame | Changes protocol semantics without Phase 5 capability negotiation. Reconnect is explicit and v1-compatible. |
| Accept both ES256 and RS256 indefinitely | Expands algorithm-confusion and operational surface without a migration need. |

## Required Tests And Evidence

### Issuance And Validation

- exact issuer/audience/scope/type/identity, 60-second bound, 5-second skew, unique `kid`/`jti`,
  and secret-free issuance logs;
- exact service/user claim allowlists and cardinality, forbidden aliases/authority, ticket/header
  byte and property limits, and rejection before unbounded decode/allocation;
- wrong issuer, audience, scope shape, type, identity, missing expiry, duplicate claims, `none`,
  algorithm substitution, unknown/removed key, stale JWKS, and invalid TLS;
- user exchange requires a live server-side IdentityServer session and rejects expired, revoked,
  disabled, wrong-tenant, wrong-origin/CSRF, bearer-only, and validly forged shared-HMAC claims;
- Service A cannot claim Service B, users cannot register service routes, and registration metadata
  cannot override validated claims;
- exact v1 case-sensitive UTF-8 service names, lowercase SHA-256 client IDs, and existing FNV-1a
  routing values remain byte-for-byte compatible, including known collision cases;
- artifact and runtime scans prove transport private signing material is absent from Hub and every
  service workload.

### Bootstrap And Renewal

- cold start in both Hub/IdentityServer orders and no Bolt/token circular dependency;
- approved workload-bootstrap mechanism and rejection of every unapproved bootstrap path;
- IdentityServer, JWKS, and network outages with no HMAC fallback;
- renewal single-flight, jitter, cancellation, make-before-break registration, bounded headroom and
  batches, drain admission, explicit unary/stream termination, renewal failure, concurrent pool
  growth, and no socket surviving expiry.

### Rotation And Revocation

- pinned TLS/origin bootstrap, signed manifest verification, key prepublication, activation,
  overlap, retirement, scheduled rollover, and emergency compromise without authentication outage;
- durable manifest and revocation high-water survives restart/redeploy and rejects lower epochs,
  same-version/different-content replay, wrong origin, wrong key purpose, and algorithm substitution;
- revoked `jti`, disabled principal, compromised `kid`, stale/replayed/future epoch, invalid feed,
  feed byte/item limits, push loss, and polling fallback;
- 30-second status lifetime and accepted-age limits, 5-second poll interval, 3-second poll deadline,
  immediate new-authentication rejection, and closure of every existing socket within 5 seconds
  after status freshness expires;
- propagation to every Hub within 15 seconds and closure of matching sockets within 60 seconds;
- dedicated JWKS polling satisfies the 15-second contract under unknown-key bursts and outages;
- churn and cleanup tests leave no stale security-index entries or retained ticket material and
  enforce per-record, item-count, and aggregate-memory ceilings.

### Secure Channel And Operations

- WSS server validation, workload mTLS acceptance/rejection and certificate rotation;
- browser query-token redaction across ASP.NET, Seq, and OpenTelemetry, plus every proxy/ingress
  layer present in the verified publication topology;
- disjoint legacy/asymmetric `typ`/issuer/audience/algorithm/keyset dispatch, ambiguous-token
  rejection, and proof that asymmetric verification failure never falls back to HMAC;
- signed rollout manifest with exact UTC deadline, build/schema versions, cohort evidence, bounded
  dual-scheme window, enforced legacy rejection, and old-HMAC rotation/revocation evidence;
- pinned Phase 0 baseline and candidate-algorithm, handshake, renewal, reconnect-storm, CPU,
  allocation, working-set, throughput, index-memory, and p99 measurements satisfy approved guards.

## Approval Checklist

- [ ] Phase 0 is marked `Verified` with complete live evidence.
- [ ] A production-equivalent Phase 0 baseline exists and every proposed threshold is approved or replaced.
- [ ] Exactly one of ES256 or RS256 is approved from signer compatibility and benchmark evidence.
- [ ] Transport, revocation, and trust-manifest signer custody and non-exportability are approved.
- [ ] Workload bootstrap has an owner, fixed UTC deadline, approved decision, and negative tests.
- [ ] IdentityServer self-issuance and exclusive live user-session proof are approved.
- [ ] Initial TLS/origin trust, signed manifest, JWKS/revocation polling, and durable rollback high-waters are approved.
- [ ] Exact user/service claim allowlists, cardinality, ceilings, and v1 route derivation are approved.
- [ ] Signed rollout cohorts, versions, UTC HMAC-disable deadline, and security-qualified rollback are approved.
- [ ] Tests prove asymmetric failure never falls back and the HMAC verifier/key are removed and revoked before the Phase 1 security gate.
- [ ] Test, observability, and performance evidence owners are assigned.

Until every item is checked and this decision is explicitly accepted, this document is research
input only and does not authorize implementation.

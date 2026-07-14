---
title: "Bolt Protocol, Hub, and Media Remediation Plan"
date: 2026-07-14
category: workflow-issues
module: Bolt
problem_type: workflow
component: transport
severity: critical
applies_when:
  - "Implementing or reviewing Bolt protocol, Hub, or Media audit remediation"
  - "Determining the security boundary, implementation order, or release gate for Bolt"
tags: [bolt, bolt-hub, bolt-media, remediation, tailscale, performance]
status: active
---

# Bolt Protocol, Hub, and Media Remediation Plan - 2026-07-14

**Status:** Reset after architecture review. The previous ten-phase plan and its certificate-heavy Phase 0 deployment gate are superseded.
**Source audit:** `docs/solutions/workflow-issues/bolt-protocol-hub-media-audit-2026-07-12.md`
**Superseded deployment gate:** `docs/solutions/workflow-issues/bolt-phase0-deployment-gate-2026-07-13.md`

## Goal

Make the core Bolt RPC, streaming, and pub/sub path correct, bounded, observable, and faster and more memory-efficient than tuned gRPC for the XFramework workloads we actually run.

Bolt Media and multi-Hub scaling are optional capability tracks. They do not block release of the core transport while Media is disabled and the Hub is intentionally limited to one replica.

## Architecture Boundary

Bolt must not own deployment certificates, private certificate authorities, certificate rotation, host exposure, or network ACLs.

| Owner | Responsibilities |
|---|---|
| Bolt Protocol and Client | Framing, parsing, negotiated limits, deadlines, cancellation, backpressure, buffer ownership, and transport-independent client behavior |
| Bolt Hub | Authentication result consumption, service identity binding, route/topic authorization, quotas, routing, health, and low-cardinality telemetry |
| IdentityServer | Central service and user authentication, short-lived transport tokens, signing, revocation, audience, and scope policy |
| Tailscale and deployment | Encrypted environment connectivity, ACLs, host exposure, DNS, and TLS termination through Tailscale Serve when `wss://` or `https://` is required |
| Application services | Business handlers and service-specific authorization |

Deployment rules:

1. Dev, staging, and production Bolt endpoints are reachable only through the tailnet or an isolated internal workload network. They are not published on unrestricted host interfaces.
2. Internal containers connect directly with `ws://bolt-hub:<internal-port>` on a private, non-published Docker network.
3. External tailnet clients connect through host-level Tailscale Serve. Serve terminates `wss://` and proxies to a Docker backend published only on `127.0.0.1`; Tailscale Funnel is disabled. No application container receives the certificate or private key.
4. IdentityServer uses the same boundary: internal HTTP on the workload network and Tailscale Serve HTTPS to a loopback-only backend when external access is required.
5. If another environment requires workload-to-workload encryption, its deployment layer supplies a service mesh, sidecar, or equivalent. Bolt containers still do not manage certificates.
6. IdentityServer tokens remain mandatory. Tailscale ACLs protect external ingress only; they do not provide per-service identity for containers on the same host and do not replace application authentication, audiences, scopes, service identity binding, or topic authorization.
7. A future public-internet deployment requires an approved TLS ingress. Tailnet-only assumptions must not silently extend to public endpoints.

## Keep, Remove, and Defer

### Keep

- Enforced reserved service identity binding and default-deny route/topic authorization.
- Central IdentityServer-issued service authentication and short-lived transport tokens.
- No caller-held Hub signing key and no service credential sent through Bolt to bootstrap Bolt authentication.
- Timeout coverage around send/enqueue and response wait.
- Bounded frame, connection, pending-RPC, stream, subscription, and media limits.
- One Hub replica until multi-Hub routing exists.
- Bolt Media disabled by default.
- Authenticated health and smoke tests that exercise a real RPC and pub/sub path.

### Remove from the Bolt release gate

- Hub and IdentityServer private TLS keys mounted into application containers.
- Private CA installation in every Bolt client container.
- Bolt-specific certificate generation, chain validation, hostname validation, and rotation workflows.
- The direct-Kestrel/no-intermediary attestation and TLS-specific synthetic evidence.
- Root watchdog, lease, and sealed-LKG machinery that exists only to enforce the superseded certificate topology.
- Image-provenance and general deployment-hardening requirements as Bolt protocol acceptance criteria. Those may remain platform controls, but they are owned by the deployment pipeline.

Removal must be done as a reviewed cleanup. Existing fail-closed controls are not deleted until the replacement Tailscale-only exposure and ordinary rollback path are proven.

### Defer without blocking core Bolt

- Multi-Hub operation: keep one replica. Address H20 only when horizontal scale is required.
- Protocol v2 negotiation: address H17 before the first incompatible wire change, not as a prerequisite for current wire-compatible fixes.
- Bolt Media: keep it disabled. H10-H14, M6-M11, and L3 move to a separate product track and must pass their own gate before enablement.

## Phase Map

| Phase | Outcome | Main findings |
|---|---|---|
| 0 | Simplify the deployment boundary and close service identity around Tailscale plus IdentityServer | C1, H3, H4, M17 |
| 1 | Fix core correctness and bounded-resource defects | H1-H9, H15-H16, H18-H19, H21, H23, M1-M5, M12-M16, L2, L5 |
| 2 | Establish fair measurements and optimize proven hot paths | H22, L1, L4, performance portions of H15-H16 |
| Optional A | Enable multi-Hub and protocol evolution only when required | H17, H20 |
| Optional B | Make Bolt Media functional and secure before enabling it | H10-H14, M6-M11, L3 |

## Phase 0 - Tailscale Deployment Boundary

### Deliverables

1. Verify or complete the centralized transport-identity path: each service obtains a short-lived, audience-restricted Bolt token from IdentityServer over HTTP before connecting; the Hub validates IdentityServer's asymmetric signature and claims; callers hold no Hub-verification signing key; and no `ClientSecret` is sent through Bolt.
2. Fail closed outside explicit local-test configuration when the legacy local HMAC token issuer or Bolt-based service-token bootstrap path is selected.
3. Remove Kestrel certificate paths, private-key mounts, client CA mounts, and certificate bootstrap logic from Bolt Hub, IdentityServer, and Bolt client Compose configuration.
4. Use internal `ws://`/`http://` service names only on a private Docker network that is not exposed outside the host.
5. Publish Hub and externally required IdentityServer backends only on host loopback. Configure persistent Tailscale Serve WSS/HTTPS listeners to proxy to those loopback ports, and keep Funnel disabled. Do not publish an alternate direct host port.
6. Define minimum external-ingress Tailscale ACLs for operators and approved nodes. Deny unrelated tailnet identities; continue using IdentityServer tokens for service-level authorization.
7. Keep IdentityServer token validation, service identity binding, topic authorization, quotas, and Hub token redaction unchanged. Confirm that Tailscale Serve and host diagnostics do not persist the Bolt `access_token` query value; otherwise block that external path until a non-replayable handshake ticket is used.
8. Replace the Phase 0 certificate workflow with a normal deployment sequence: migrate, deploy Hub plus IdentityServer and Communications, run authenticated RPC/pub/sub smoke tests, observe briefly, then deploy remaining clients.
9. Transition in a bounded outage: freeze Bolt deployment, stop the Hub, retain the complete old TLS deployment bundle for rollback, activate Serve/ACLs and the new loopback/private-network configuration, deploy, smoke test, and prove rollback to the complete old bundle. After the new topology is accepted as the ordinary rollback baseline, decommission the old watchdog/lease machinery and delete legacy certificate material.
10. If activation or rollback fails, stop the Hub. Do not create a replacement Bolt-specific root recovery subsystem.

### Acceptance Gate

- No application container receives a TLS private key or private CA file.
- No deployed service contains a Hub-signing secret, can mint another service's identity, or sends its client secret through Bolt. A hostile duplicate reserved-service registration is rejected.
- A service can obtain a short-lived transport token from IdentityServer and the Hub rejects wrong issuer, audience, scope, identity, expiry, and signature.
- Docker publishes the Serve backends only on loopback; no direct Bolt/IdentityServer port is reachable on a host network interface.
- Tailscale Serve exposes only the intended WSS/HTTPS listeners, Funnel is disabled, and no configured diagnostic sink persists Bolt query tokens.
- An allowed tailnet client can connect; a denied tailnet identity cannot.
- A real service token can register the correct service identity and complete RPC and pub/sub smoke tests.
- Plain internal endpoints are unreachable outside the private workload network.
- Restarting `tailscaled` restores the persistent Serve configuration without exposing a plaintext fallback.
- The complete old TLS bundle can be restored during transition; after acceptance, the new image/configuration set is the ordinary rollback baseline.

## Phase 1 - Core Correctness and Resource Bounds

### Work Packages

1. **RPC lifecycle:** eliminate pooled completion ABA reuse, arm deadlines before send, propagate send failure, make late/duplicate responses harmless, and retire failed pool members.
2. **Connection and stream isolation:** preserve response affinity, prevent one unread logical stream from blocking the receive loop, bound registration/reconnect, and clean up streams/subscriptions deterministically.
3. **Admission and memory bounds:** enforce byte-based queue budgets, per-principal concurrency/rate limits, small receive buffers, immediate large-buffer return, and bounded dispatch.
4. **Ownership and copies:** give every pooled buffer one deterministic owner/return path, remove finalizer-dependent returns, and reduce full-frame copies where measurements prove value.
5. **Hub authorization and state:** default-deny topics, bound durable subscribers, use explicit tenant/soft-delete predicates for actor authorization, keep authorization/model/query failures local to the offending frame, avoid per-message authorization database work, and bound service-discovery retention/evaluation.
6. **Deadline semantics:** add an absolute request deadline and exact-request cancellation contract. Reject expired work before dispatch, cancel the remote handler token, and make late/duplicate cancellation idempotent. Introduce the minimum negotiated protocol capability needed for this change; broader protocol evolution remains optional.
7. **Telemetry:** expose queue bytes, pending RPCs, active streams, rejects, send failures, route misses, reconnects, and retained pooled bytes without principal/topic cardinality in metric labels.

### Acceptance Gate

- Deterministic tests cover blocked enqueue, timeout-before-send, late response after reuse, duplicate response, send-loop failure, two-connection response affinity, unread streams, and reconnect/disposal races.
- Deadline tests cover pre-dispatch expiry, caller cancellation during remote execution, duplicate/late cancellation, and bounded cleanup of the exact handler.
- Saturation and malicious-frame tests prove configured byte and concurrency ceilings cannot be exceeded materially.
- PostgreSQL-backed authorization tests cover tenantless service principals, active membership, disabled/deleted credentials, wrong-tenant denial, and proof that an authorization exception rejects one frame without closing the shared service connection.
- A 30-minute saturation run plus a 10-minute overload run completes without unbounded queue, pending-call, stream, or retained-pool growth.
- Full Bolt tests pass with zero skipped mandatory integration tests.
- An independent reviewer checks the final diff against the source findings before merge.

## Phase 2 - Measure and Optimize

### Deliverables

1. Correct benchmark operation accounting and request-level latency collection.
2. Compare equivalent typed and raw work, payloads, explicit channel-count sweeps, serialization, status validation, and security/network topology.
3. Run Bolt and tuned gRPC on the same hosts and Tailscale path for deployed comparisons; use the same direct/local topology for isolated implementation benchmarks.
4. Run closed-loop and open-loop load with coordinated-omission-corrected request-level HDR p50/p95/p99/p99.9. Measure errors, CPU, allocated bytes per successful operation, working set, GC pauses, queue time, and copy bytes.
5. Optimize only measured bottlenecks. Likely candidates are frame copies, receive-buffer retention, route selection, serialization, and fanout ownership.
6. Keep a stable regression suite for small RPC, 1 KB RPC, 64 KB RPC, streaming, and pub/sub fanout at representative concurrency.

### Acceptance Gate

- The harness produces correct per-operation counts and request-level percentiles.
- Bolt and gRPC run with equivalent work and topology, including tuned gRPC channel/HTTP2 settings.
- Results use at least five independent runs on a stable runner and continue until the 95% confidence-interval half-width for primary throughput, p99, and allocation results is at most 5%.
- Saturation and overload comparisons include enough steady-state time to expose queueing and memory retention rather than reporting only short benchmark iterations.
- Core release requires no material regression from the accepted Bolt baseline.
- A claim that Bolt beats gRPC is made only for workloads where the complete result supports it. The engineering goal remains higher throughput and lower memory across the XFramework workload matrix, not an unqualified universal claim.

## Optional A - Protocol Evolution and Multi-Hub

Start only when an incompatible wire feature or a second Hub replica is needed.

- Extend the minimum deadline capability negotiation from Phase 1 with collision-safe command identity and any additional version/capability exchange before other incompatible frames ship.
- Add an instance-aware backplane for routing, live pub/sub, presence, and service discovery.
- Require two-Hub disconnect, reconnect, durable replay, transient pub/sub, and presence tests before increasing replicas.

Until then, one Hub is an explicit supported constraint, not an unfinished release blocker.

## Optional B - Bolt Media

Media remains off until a separately reviewed two-peer vertical slice works end to end.

Minimum enablement gate:

- Real browser capture, encode, send, remote decode, playback, hold, and cleanup.
- Bounded sequence/NACK/FEC behavior under loss, reordering, duplicates, and malicious gaps.
- Authenticated, fail-closed encryption with per-call state and downgrade/replay tests.
- Membership and key convergence for the supported call size.
- Bounded media queues and recipient fanout.

Congestion control, simulcast, QUIC/WebTransport, recording, and group calls are added only after the basic two-peer path is correct and measured.

## Practical Rollout

For each core phase:

1. Merge one bounded PR with focused and full Bolt tests.
2. Have an independent agent review the exact final diff against this plan and the audit findings.
3. Deploy Hub, IdentityServer, and Communications as the canary set.
4. Run authenticated RPC, pub/sub, reconnect, and health smoke tests.
5. Observe for reconnect storms, queue growth, send failures, and authorization errors.
6. Deploy remaining services or restore the previous image/configuration set.

No phase requires a custom root trust hierarchy, certificate ceremony, or hundreds of lines of deployment evidence to be considered complete.

## Immediate Order

1. Finish and merge the current Hub authorization-model loading/tenant-context fix for H19 as its own PR.
2. Implement Phase 0 as a separate cleanup PR; do not mix deployment-boundary removal into the H19 hotfix.
3. Re-audit the remaining Phase 1 findings against current `develop`, because several earlier containment fixes may already satisfy part of them.
4. Implement Phase 1 in small correctness/ownership packages, reviewing after each package.
5. Build the corrected comparison harness before doing broad performance refactors.

## Completion Definition

Core Bolt is ready when Phase 0, Phase 1, and Phase 2 pass; no Critical or High finding in the enabled core RPC/stream/pub-sub surface remains open; deployment exposure is owned by Tailscale; and measured results support the published workload-specific performance claims.

Media and multi-Hub completion are separate decisions and do not block the core transport while their kill switches and single-replica constraint remain enforced.

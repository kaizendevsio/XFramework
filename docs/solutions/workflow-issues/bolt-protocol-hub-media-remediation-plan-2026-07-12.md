---
title: "Bolt Protocol, Hub, and Media Remediation Plan"
date: 2026-07-15
category: workflow-issues
module: Bolt
problem_type: workflow
component: transport
severity: critical
applies_when:
  - "Implementing or reviewing Bolt protocol, Bolt client, Bolt Hub, or Bolt Media audit remediation"
  - "Prioritizing Bolt correctness, resource efficiency, or performance work"
tags: [bolt, bolt-client, bolt-hub, bolt-media, correctness, performance]
status: active
---

# Bolt Protocol, Hub, and Media Remediation Plan

**Source audit:** `docs/solutions/workflow-issues/bolt-protocol-hub-media-audit-2026-07-12.md`

## Purpose

Make Bolt's core RPC, streaming, and pub/sub paths correct, bounded, observable, and measurably faster and more memory-efficient than tuned gRPC for the XFramework workloads that use them.

This is a Bolt engineering plan. It covers the wire protocol, clients, Hub, browser client, and the narrow IdentityServer token contract consumed by Bolt. It is not a deployment-platform, network-security, certificate-management, Portal, or infrastructure-remediation plan.

The engineering target is to outperform tuned gRPC across the representative XFramework workload matrix. Performance claims must remain workload-specific until equivalent benchmark evidence supports anything broader.

## Scope

### In Scope

- Bolt framing, parsing, serialization, command dispatch, and protocol invariants.
- RPC lifecycle, deadlines, cancellation, send failure, and response correlation.
- Connection pooling, reconnect behavior, stream isolation, subscriptions, and cleanup.
- Backpressure, admission control, queue budgets, pooled buffers, copies, and memory retention.
- Bolt Hub authentication result handling, service identity binding, authorization, routing, quotas, and bounded state.
- Bolt-specific health signals and low-cardinality telemetry needed to operate and optimize the transport.
- Fair Bolt-versus-gRPC benchmarks and optimization of measured Bolt bottlenecks.
- Bolt Media only as a separate, disabled-by-default product track.

### Out of Scope

- Tailscale Serve, Tailscale ACLs, Funnel, tailnet membership, DNS, firewalls, and host networking.
- TLS termination, certificate issuance, private CAs, certificate rotation, and reverse proxies.
- Docker host-port publication, deployment workflows, rollout attestations, rollback qualification, watchdogs, leases, and image-provenance policy.
- General IdentityServer business logic. IdentityServer changes are in scope only when the Bolt token contract itself requires them.
- Portal UI timeouts or other application-level workarounds for transport defects.
- General service deployment, orchestration, and infrastructure health.

Those concerns may have their own plans and owners. They must not block protocol correctness or performance work, and they must not be added to Bolt release acceptance criteria.

## Prioritization Rules

1. Revalidate every audit finding against current `develop` before changing code. Close findings already fixed and update stale evidence.
2. Prioritize reproducible defects on enabled RPC, streaming, pub/sub, and browser-client paths.
3. Require a concrete failure mode, production-relevant workload, test, profile, or benchmark before adding complexity.
4. Fix correctness and hard resource bounds before micro-optimizing hot paths.
5. Optimize only after measurement identifies a material bottleneck.
6. Keep disabled or unsupported capabilities out of the core release gate.
7. Prefer small changes that preserve the existing wire contract. Introduce protocol evolution only when a required fix cannot be made compatibly.

## Phase 1 - Protocol and Client Correctness

### Objective

Make every enabled Bolt client operation complete, fail, cancel, time out, and release resources deterministically under normal operation, transport failure, and realistic saturation.

### Work Packages

1. **RPC lifecycle**
   - Eliminate pooled completion reuse races.
   - Arm the request deadline before enqueue/send can block.
   - Propagate enqueue and transport-send failures to the caller.
   - Make late and duplicate responses harmless.
   - Retire failed connection-pool members predictably.

2. **Cancellation and deadlines**
   - Define an absolute request deadline.
   - Propagate caller cancellation to the exact remote request when supported.
   - Reject expired work before handler dispatch.
   - Make duplicate or late cancellation idempotent.

3. **Connection, stream, and subscription lifecycle**
   - Apply an end-to-end registration timeout.
   - Preserve response affinity for multi-connection clients.
   - Prevent an unread logical stream from blocking unrelated traffic.
   - Remove closed streams and subscriptions deterministically.
   - Make reconnect and disposal races safe and bounded.

4. **Buffer ownership and protocol invariants**
   - Give each rented buffer exactly one owner and return path.
   - Remove finalizer-dependent pool returns.
   - Ensure writers cannot emit frames that readers reject.
   - Align browser codec validation and safety limits with the .NET client.
   - Reduce copies only where profiling demonstrates material value.

5. **Client-side admission and memory bounds**
   - Bound send queues by bytes, not only item count.
   - Bound pending RPCs, active streams, and subscriptions.
   - Avoid retaining large receive buffers after large frames.
   - Keep overload behavior explicit and observable.

### Source Findings

`H1`, `H5-H9` for client/core paths, `H15-H16`, `H23`, `M1-M5`, and `L5`.

### Acceptance Gate

- Focused deterministic tests reproduce each corrected defect before or alongside the fix.
- Tests cover blocked enqueue, timeout before send, send-loop failure, late/duplicate response, response affinity, unread streams, reconnect, cancellation, and disposal.
- Configured queue, pending-call, stream, subscription, frame-size, and retained-buffer bounds are enforced under realistic saturation.
- Full mandatory Bolt protocol and client tests pass without newly skipped coverage.
- An independent reviewer compares the final diff with the named findings and this phase before merge.

## Phase 2 - Bolt Hub Correctness and Bounded State

**2026-07-29 progress:** destination generated-handler service-token authorization and sender binding, explicit-recipient Push routing, per-principal RPC/request-byte limiting, and required XFramework Hub topic authorization are implemented under the focused authorization and rate-limit plan. Remaining Phase 2 items continue to be tracked independently by their concrete audit findings.

### Objective

Make the Hub authorize, route, isolate, and account for enabled Bolt traffic correctly while keeping CPU, memory, database work, and retained state bounded.

### Work Packages

1. **Authentication and service identity**
   - Consume centrally issued IdentityServer transport tokens.
   - Validate the expected issuer, audience, signature, expiry, scope, and service identity.
   - Prevent a caller from registering or joining another reserved service identity.
   - Keep signing authority out of ordinary service callers.

2. **Authorization and request isolation**
   - Default-deny protected routes and topics.
   - Apply explicit tenant, membership, credential-status, and soft-delete predicates.
   - Ensure authorization or handler failure rejects only the offending frame or request.
   - Keep Bolt-local discovery reads under the same authorization contract as equivalent service APIs.

3. **Hub admission and backpressure**
   - Enforce byte budgets and per-principal concurrency or rate limits where needed.
   - Bound request, push, stream, subscription, and pub/sub dispatch.
   - Keep one slow consumer from retaining unbounded Hub memory or blocking unrelated traffic.

4. **Bounded Hub state**
   - Bound durable subscribers, service discovery, presence, and route evaluation.
   - Remove disconnected and expired state deterministically.
   - Avoid quadratic recipient selection and unsafe concurrent removal.
   - Avoid per-message identity or database work when an equivalent safe cached decision is valid.

5. **Operational signals**
   - Report connection state, send-loop and receive-loop state, pending sends, queue bytes, pending RPCs, active streams, rejects, route misses, reconnects, and retained pooled bytes.
   - Keep metric labels low-cardinality and exclude tokens, principals, and topic values.
   - Make readiness reflect whether the Hub can accept and process traffic, not merely whether it once connected or registered.

### Source Findings

`C1`, `H2-H3`, `H7-H9` for Hub paths, `H18-H19`, `H21`, `M3`, `M12-M16`, and `L2`.

### Acceptance Gate

- Tests prove reserved service identities cannot be joined, replaced, or intercepted by another caller.
- Authentication tests cover wrong issuer, audience, signature, scope, identity, and expiry.
- PostgreSQL-backed authorization tests cover tenantless service principals, active membership, disabled or deleted credentials, wrong-tenant denial, and request-level failure isolation.
- Saturation tests prove configured byte, concurrency, subscriber, discovery, and retained-state bounds.
- Health and telemetry tests prove failed send/receive loops and blocked queues are visible without high-cardinality labels or secret leakage.
- Full mandatory Hub integration tests pass without newly skipped coverage.
- An independent reviewer compares the final diff with the named findings and this phase before merge.

## Phase 3 - Benchmark and Optimize

### Objective

Produce a defensible Bolt-versus-gRPC comparison, then improve Bolt throughput, latency, allocations, CPU, and working-set efficiency based on measured bottlenecks.

### Benchmark Requirements

1. Compare equivalent typed and raw work, payloads, serialization, status validation, and connection counts.
2. Use tuned gRPC channel and HTTP/2 settings appropriate to each workload.
3. Measure small RPC, 1 KB RPC, 64 KB RPC, streaming, and pub/sub fanout at representative concurrency.
4. Run both transports on identical hardware, runtime, topology, and load-generation conditions.
5. Report request-level throughput, errors, p50, p95, p99, and p99.9 latency, CPU, allocated bytes per successful operation, working set, GC pauses, queue time, and copy bytes.
6. Include steady-state saturation and overload runs long enough to reveal queueing and memory retention.
7. Correct operation accounting and coordinated omission before using results for product claims.

### Optimization Order

Optimize only bottlenecks identified by profiles and benchmark evidence. Likely areas to measure include:

- Full-frame and serialization copies.
- Receive-buffer retention and pool behavior.
- Route and recipient selection.
- Queue contention and wakeups.
- Pub/sub fanout ownership.
- Browser codec allocations.
- Repeated authorization or discovery work on hot paths.

### Source Findings

`H22`, `L1`, `L4`, and the measured performance portions of `H2`, `H15-H16`, `H21`, `M3`, `M5`, and `M16`.

### Acceptance Gate

- The harness reports correct per-operation counts and request-level latency distributions.
- Bolt and gRPC perform equivalent work with documented tuning and no hidden topology advantage.
- Results are repeatable across at least five independent runs on a stable runner.
- The primary throughput, p99 latency, and allocation results have sufficiently narrow confidence intervals to support decisions.
- Core Bolt has no material regression from its accepted baseline.
- Claims that Bolt beats gRPC are published only for workloads where the complete results support them.

## Separate Track A - Bolt Media

Bolt Media is not a core Bolt release blocker while it remains disabled. Do not mix Media fixes into core RPC, streaming, pub/sub, or Hub performance PRs.

Before enabling Media, deliver one reviewed two-peer browser vertical slice that provides:

- Real capture, encode, send, remote decode, playback, hold, and cleanup.
- Bounded sequence, NACK, FEC, jitter, and queue behavior under realistic loss and reordering.
- Authenticated fail-closed encryption without silent downgrade.
- Correct identity, capability, membership, and key-state convergence.
- Bounded recipient fanout and memory use.
- Browser integration tests that exercise actual media flow rather than compilation alone.

Source findings: `H10-H14`, `M6-M11`, and `L3`.

Congestion control, simulcast, recording, group calls, QUIC/WebTransport, and direct peer-to-peer operation remain deferred until the basic two-peer path works and measurements justify them.

## Separate Track B - Protocol Evolution and Multi-Hub

These are capability projects, not unfinished core-release work.

- Address protocol version and capability negotiation before the first incompatible wire change.
- Replace collision-prone command identity only when compatibility requirements justify a wire change.
- Keep one Hub replica as the supported topology until an instance-aware routing and pub/sub backplane exists.
- Require multi-Hub routing, disconnect, reconnect, durable replay, transient pub/sub, and presence tests before enabling a second replica.

Source findings: `H17` and `H20`.

## Explicitly Separate Issues

The following audit findings are valid concerns but are not part of this Bolt remediation plan:

- `H4`: deployment transport encryption and plaintext WebSocket exposure.
- `M17`: deployment port publication and health-probe mismatch.

Track them in the deployment or platform backlog. Their resolution must not introduce certificate, networking, ACL, rollout, or host-management responsibilities into Bolt Protocol, Bolt Client, or Bolt Hub.

## Execution Model

For each core phase:

1. Revalidate the named findings against current `develop`.
2. Group only closely related fixes into a bounded PR.
3. Add focused regression tests for corrected behavior.
4. Run the focused suite and the full mandatory Bolt suite.
5. Have an independent agent review the exact final diff against the source findings and phase acceptance gate.
6. Update the audit finding status and evidence after the fix is verified.

Deployment and environment changes require their own explicit plan and approval. They are never implied by approval of this document.

## Completion Definition

Core Bolt remediation is complete when:

- Phases 1 and 2 have no open Critical or High finding on enabled RPC, streaming, pub/sub, browser-client, or Hub paths.
- Enabled paths enforce practical, tested limits on queues, pending work, streams, subscriptions, buffers, and retained Hub state.
- Phase 3 provides a fair, repeatable Bolt-versus-gRPC benchmark and the accepted XFramework workload results meet the approved performance targets.
- Mandatory Bolt and Hub tests pass without newly skipped integration coverage.
- Published capability and performance claims match verified behavior and evidence.

Bolt Media, incompatible protocol evolution, and multi-Hub operation have separate completion decisions and do not block core Bolt while they remain disabled or unsupported.

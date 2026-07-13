---
title: "Bolt Protocol, Hub, and Media Remediation Plan"
date: 2026-07-13
category: workflow-issues
module: Bolt
problem_type: workflow
component: transport
severity: critical
applies_when:
  - "Implementing or reviewing Bolt protocol, Hub, or Media audit remediation"
  - "Determining phase order, acceptance gates, or evidence requirements for Bolt work"
tags: [bolt, bolt-hub, bolt-media, remediation, rollout, performance]
status: active
---

# Bolt Protocol, Hub, and Media Remediation Plan - 2026-07-12

**Status:** In progress - Phase 0 containment merged through PR #352 and passed its committed Linux CI gate; P0-R21 and P0-R22 are fixed locally and await final review/CI while the one-time recovery bootstrap and live staged evidence remain open
**Plan review:** `PASS` on 2026-07-13 after independent protocol/performance, Hub/security, Media, and integrated-plan reviews. This does not pass any implementation or deployment gate.
**Source audit:** `docs/solutions/workflow-issues/bolt-protocol-hub-media-audit-2026-07-12.md`
**Audit baseline:** `origin/develop` at `18189df6fc486733819c49d8d8095033247d3186`
**Phase 0 merge:** PR [#352](https://github.com/kaizendevsio/XFramework/pull/352), merge commit `fb169a530d6e80d64aeab7c73029fabea08d3152`; committed Ubuntu gate [29229428707](https://github.com/kaizendevsio/XFramework/actions/runs/29229428707) passed
**Execution model:** One phase at a time, one reviewable PR per bounded deliverable, with an independent sub-agent verification gate before a phase is marked complete.

## Goal

Make Bolt a secure, correct, bounded, observable, and demonstrably high-performance transport for XFramework RPC, pub/sub, streaming, and media.

The performance claim must be workload-specific. No transport can credibly be called fastest for every payload, topology, security mode, and concurrency level. The release target is to beat or match tuned gRPC within the Quantitative Test Envelope while using materially less memory, with evidence from a fair and reproducible benchmark suite.

## Non-Negotiable Rules

1. Security and correctness take precedence over preserving a microbenchmark number.
2. No service credential or signing authority may be sent through Bolt to bootstrap Bolt authentication.
3. No queue, stream, pending-call set, replay set, FEC group, or registry may be bounded only by item count when item sizes vary materially.
4. Every pooled buffer must have explicit, deterministic ownership and exactly one return path.
5. A logical stream, handler, subscriber, or media recipient must never block the physical connection demultiplexer indefinitely.
6. Protocol changes require explicit version/capability negotiation and rolling-upgrade tests.
7. Bolt Media remains behind a kill switch until its two-peer, encrypted, loss/reorder, and cleanup gates pass.
8. Horizontal Hub scaling remains disabled until two-Hub routing, pub/sub, media, and presence tests pass.
9. Marketing or architecture documentation must not claim capabilities or performance that the release gate does not prove.
10. A phase is not complete until an independent sub-agent checks its diff and evidence against this plan and the source audit.
11. Phase 0 is staging containment, not a production service trust boundary. Production promotion remains blocked until Phase 1 removes the shared Hub-verification signing key from callers.

## Quantitative Test Envelope

These are minimum gates. A phase may tighten them through an ADR, but may not weaken them without a documented security/correctness justification and a new independent review.

- Core RPC matrix: 64 B, 1 KB, and 64 KB payloads; concurrency 1, 8, 64, and 256; TLS; controlled 1 ms and 10 ms RTT.
- Saturation: the highest offered load with less than 0.1% errors and p99 no greater than twice unloaded p99; also test 125% of that offered load to prove bounded overload behavior.
- Reproducibility: at least ten independent launches, throughput coefficient of variation at or below 5%, and reported 95% confidence intervals.
- Resource soak: at least 60 minutes at saturation plus 15 minutes at 125% overload, followed by a 60-second drain/settle window.
- Memory recovery: after the settle window, retained pool bytes must be no greater than the pre-test baseline plus fixed base receive buffers plus 5% of the configured global byte budget; working set must be no greater than the pre-test baseline plus 10% or 64 MB, whichever is larger.
- Early-phase performance guard: no greater than 5% regression in p99, throughput, CPU, or allocated bytes against the Phase 2 baseline without an explicit security/correctness waiver and assigned follow-up.
- Active revocation: a disabled or revoked already-connected principal must be disconnected or reauthenticated within 60 seconds.
- Registry scaling: compare 10,000 and 100,000 records; normalized evaluation time per record at 100,000 must be no more than 1.25 times the 10,000-record result.
- Initial media envelope: two-peer calls run for at least 60 seconds; group tests use 8 video participants and 16 audio participants; hold stops outbound media within 250 ms.
- Adaptive media: after a controlled bandwidth step, sender bitrate must converge below 90% of available bandwidth within 3 seconds and recover within 5 seconds; with 1% loss, 5% packet reordering, and up to 100 ms late arrival, a 100 ms jitter target must stay at or below 120 ms at p99; simulcast layer changes complete within 2 seconds.
- Benchmark load search: offered load increases in steps no larger than 10%; steady-state cells use at least 2 minutes of warmup, 10 minutes of measurement, 100,000 completed operations, and ten independent launches unless BenchmarkDotNet's statistically stricter policy applies.
- Statistical quality: the 95% confidence-interval half-width must be at most 5% of the point estimate for primary throughput, p99, allocated-bytes, and working-set comparisons; failures, timeouts, cancellations, overload rejections, and wrong responses are reported separately and included in the release decision.
- Claim definitions: `match` means the complete 95% interval stays within a predeclared 5% non-inferiority margin; `faster` means the complete interval proves at least 10% higher throughput or 10% lower latency with no gated regression; `materially less memory` means at least 30% fewer aggregate allocated bytes per successful operation and at least 20% lower aggregate steady-state/peak working set across every participating client, Hub, and backend process.

## Phase Map

| Phase | Purpose | Findings addressed or contained | Relative size |
|---|---|---|---|
| 0 | Immediate containment | C1, H2-H4, H12-H13, H18-H20, M12-M13, M17, L1-L2 | Small |
| 1 | Transport identity and secure channel | C1, H3, H4, M12 | Extra large |
| 2 | Measurement and observability foundation | H22, L2, L4 | Medium |
| 3 | RPC, connection, and stream lifecycle correctness | H1, H6-H9, H23 containment, M1-M3 | Large |
| 4 | Resource governance and memory ownership | H2, H5, H15, H16 | Extra large |
| 5 | Versioned protocol and cross-runtime parity | H17, H23 wire semantics, M4, M5, L5 | Extra large |
| 6 | Hub pub/sub, discovery, and horizontal scaling | H18-H21 (non-media routing), M13-M16 | Extra large |
| 7 | Bolt Media core correctness and security | H10-H14, H20 (media routing), M6-M9, L3 | Extra large |
| 8 | Media adaptation and production transports | M10, M11, remaining L3 | Extra large |
| 9 | Performance optimization and release certification | H16, H22, L1, L2, L4 | Ongoing program |

Phase 0 contains risk but does not close findings whose root causes belong to later phases. Finding status must distinguish `Contained`, `Implemented`, `Verified`, and `Accepted Risk`.

Phases 3 and 4 intentionally contain only wire-compatible safety repairs before Protocol v2. The ABA, send-failure, stream-isolation, and byte-budget defects have immediate operational impact and should not wait for a new wire format. Phase 5 still precedes every new media signaling capability and all multi-node enablement.

## Current Execution Ledger

This is the authoritative implementation order. A later phase may be researched in parallel, but its production code must not merge until every earlier phase is `Verified` unless an ADR records the dependency exception and an independent reviewer approves it.

| Order | Phase | Current state | Required next decision |
|---:|---|---|---|
| 1 | Phase 0 - Immediate containment | `Contained; merged baseline plus local rollout corrections; live gates pending` | Review and merge the rollout corrections, provision the sealed watchdog bootstrap, execute the staged xeon-dev rollout, and collect the required live evidence. |
| 2 | Phase 1 - Transport identity | `Not started` | Approve the centralized asymmetric transport-token ADR and bootstrap path. |
| 3 | Phase 2 - Measurement foundation | `Not started` | Establish the fair tuned-gRPC baseline and Bolt telemetry before optimizing internals. |
| 4 | Phase 3 - Lifecycle correctness | `Not started` | Fix ABA completion ownership, timeout coverage, send semantics, affinity, stream isolation, and reconnect state. |
| 5 | Phase 4 - Resource governance | `Not started` | Enforce byte budgets and deterministic buffer ownership under saturation and slow readers. |
| 6 | Phase 5 - Protocol v2/parity | `Not started` | Approve version/capability negotiation and cross-runtime compatibility policy. |
| 7 | Phase 6 - Hub scaling | `Not started` | Make pub/sub, discovery, presence, and routing bounded and correct before enabling a second Hub. |
| 8 | Phase 7 - Media core | `Disabled` | Complete media correctness, browser wiring, and independently reviewed fail-closed encryption. |
| 9 | Phase 8 - Media adaptation | `Disabled` | Prove ABR, jitter, simulcast, and selected production transports end to end. |
| 10 | Phase 9 - Optimization/certification | `Blocked by Phases 0-8` | Optimize measured bottlenecks and publish only workload-specific claims supported by the certification matrix. |

The merged Phase 0 implementation contains only the minimum wire-compatible client containment required for bounded registration acknowledgement, registration-attempt transport and acknowledgement-buffer cleanup, baseline pending-call accounting, and process-lifetime transport-health watermarks. Registration cleanup is the inseparable failure path of source-audit finding M1: a timed-out registration must not leak its socket while retrying. The broader client lifecycle and ownership rewrite is excluded from this phase. The unread-stream, send-completion, large-response-affinity, handler-lifecycle, custom-frame ownership, retained pooled-chunk, zero chunk-size, general failed-pool-member retirement/reconnect cleanup, connection retirement, and buffer-lifetime findings remain open for Phases 3 and 4.

### Phase 0 Deployment Work Packages

The following packages must be executed in order. Code-level completion does not advance the phase unless its runtime evidence is captured.

| Package | Scope | State | Exit evidence |
|---|---|---|---|
| P0-A | Containment code: `Enforce`, reserved identity binding, temporary quotas, 8 MiB ceiling, default-deny authorization, socket lifetime, redaction, Media kill switch, single replica | Implemented and CI-validated; formal review pending | Bolt tests, negative identity/authorization tests, configuration tests, and the container build passed for the merged change. A formal independent review bound to the final tested tree remains required. |
| P0-B | Deployment preflight: exact `repository@sha256:digest` pins, approved repository mapping, signed build provenance, run-scoped full-stack manifests, effective/runtime Kestrel listeners, resolved private-key mount isolation, typed non-executing env parsing, internal and real public-hostname certificate validation, fatal CA installation, exact quotas, and one running Hub | Implemented and CI-validated; live evidence pending | Adversarial verifier tests passed in committed Ubuntu CI. Registry, provenance, listener, mount, TLS, and runtime evidence must be produced by the live run. |
| P0-C | Coordinated credential rotation | Implemented; live evidence pending | The bounded `G`/`G+1` prepare, activate, convergence, expiry, finalization, and old-generation rejection state machine is automated. Execute it against xeon-dev. |
| P0-D | Staged rollout | Implemented; bootstrap pending | Legacy deployment is frozen; the workflow retains run-scoped manifests and deploys Hub, canary, then bounded batches. Provision the sealed watchdog bootstrap before execution. |
| P0-E | Authenticated synthetics and observation | Implemented; retry corrections pending review/CI | The dedicated principal, HTTPS/TLS inputs, Seq key, trace endpoint, and direct-Kestrel mode declaration are provisioned on xeon-dev. Strict bare-DTO token refresh, root-verified topology binding, qualification mode sealing, mode-specific receipts, and recovery binding are fixed locally; merge/CI and live execution remain pending. |
| P0-F | Broad rollout and recovery qualification | Implemented; live evidence pending | Batch gates, rollback drill, root-sealed LKG qualification, forced recovery, and fail-closed Hub stop are automated. Execute and retain evidence. |

### Phase 0 Rollout State Machine

1. Disable automatic push deployment and independent Hub/client deployment until Phase 0 is `Verified`; create immutable run-scoped candidate and last-known-good full-stack manifests.
2. Preflight the rendered base Compose plus digest override before any remote mutation, including approved repositories, signed source/build provenance, exact quotas, one-Hub topology, typed env input, resolved mounts, effective endpoints, and real DNS/TLS reachability.
3. Preserve current evidence, block Bolt traffic, and pre-stage credential generation `G+1` for dual validation while `G` remains accepted. Do not issue/use `G+1` until every validator and IdentityServer registration can accept both generations.
4. Run migrations, deploy the Hub only, inspect actual listeners, reject plaintext `/bolt/ws` from a peer container, and require trusted public and internal live/ready probes.
5. Deploy IdentityServer and Communications as the canary set because the authenticated synthetic exercises both service RPC and pub/sub.
6. Run the complete synthetic suite, including offline replay and Redis interruption, and verify that its unique token marker is absent from application, Seq, and trace storage. Query proxy/ingress retained stores only when root-verified publication topology includes them; otherwise retain a root-sealed direct-publication not-applicable receipt.
7. Observe the canary for the documented window; reject advancement on authentication anomalies, reconnect storms, queue growth, pool retention, send-loop failure, or p95/p99 regression.
8. Deploy remaining clients in bounded batches, rerunning runtime digest, health, and synthetic gates after each batch.
9. Inventory every running validator/client, prove it uses `G+1`, wait for the maximum accepted `G` token lifetime, revoke `G`, and prove old tokens and client secrets fail while HTTP and Bolt health remain green.
10. On failure, never restore plaintext, Audit/Off identity binding, unbounded limits, Media, generation `G`, or an unrelated/mutable image. Apply the run-scoped security-qualified full-stack manifest with `G+1`; if none exists, stop Bolt traffic and leave dependent HTTP surfaces available where safe.
11. Capture the exact manifests, provenance attestations, image digests, rotation/convergence/revocation metadata, synthetic report, health results, telemetry queries, observation decision, and rollback execution evidence in the Phase 0 artifact.

### Phase 0 Review Findings

These deployment-review items supplement the source-audit IDs and remain part of the Phase 0 gate.

| ID | Severity | Finding | Disposition |
|---|---|---|---|
| P0-R1 | High | A mutable 40-character tag or unrelated repository digest could satisfy the deployment authorization check. | Merged and CI-tested: approved repository mapping, registry-confirmed digests, signed provenance, and run-scoped state are mandatory. Live evidence pending. |
| P0-R2 | High | The full workflow deploys every service at once and lacks a Hub-to-canary-to-synthetic gate and fail-closed recovery. | Merged and CI-tested: staged promotion and forced fail-closed recovery are mandatory. Live evidence pending. |
| P0-R3 | Medium | Compose validation checked `ASPNETCORE_URLS` rather than effective Kestrel endpoints. | Merged and CI-tested verifier implementation covers named Kestrel and actual runtime listener/socket inspection. Live runtime evidence remains pending. |
| P0-R4 | Medium | Alternate secret aliases or bind mounts could expose the Hub private key to clients. | Merged and CI-tested verifier implementation covers resolved path/inode and parent-directory isolation checks. Live resolved-mount evidence remains pending. |
| P0-R5 | Medium | TLS evidence validated only the internal `bolt-hub` name, not the published hostname. | Merged and CI-tested with internal/public names and real DNS/TLS routing checks. Live evidence pending. |
| P0-R6 | Medium | Deployment workflows executed the environment file as shell code. | Implemented with typed non-executing parsing across the complete SSH consumer chain. |
| P0-R7 | Low | CA installation failure did not prevent application startup. | Implemented as startup-fatal and covered by configuration tests. |
| P0-R8 | Low | Standalone Staging configuration exposed only plaintext HTTP. | Implemented with loopback-only HTTP health and public HTTPS. |
| P0-R9 | Medium | The Phase 0 synthetic sends the fully qualified `HealthCheckRequest` type name, while generated Bolt handlers register the simple request type name. The canary RPC would fail before reaching IdentityServer. | Implemented with the generated-handler command contract and integration coverage. |
| P0-R10 | High | Deployment failures collect diagnostics but do not execute a qualified rollback or stop Bolt traffic. Shared remote pin files can be overwritten by single-service deployment. | Merged and CI-tested with run-scoped state, rollback drill, forced recovery, root-sealed LKG, and Hub stop fallback. Live evidence pending. |
| P0-R11 | High | Preflight does not prove the effective runtime boundary: actual sockets, peer plaintext rejection, real public DNS/routing, or resolved/symlinked parent mounts. | Merged and CI-tested in runtime/TLS/operational verifiers. Live evidence pending. |
| P0-R12 | Medium | Current durable synthetic subscribes before publish and does not prove offline persistence, ordered reconnect replay, no redelivery, monotonic acknowledgement, or Redis interruption recovery. | Merged and CI-tested, including bounded exact Redis ACK behavior and post-recovery durable probes. Live evidence pending. |
| P0-R13 | Medium | The env parser is non-executing but untyped; unsafe values can reach shell/SSH consumers. | Implemented and adversarially tested with typed, non-executing parsing and strict consumer contracts. |
| P0-R14 | High | Existing workflows generate missing secrets but do not atomically rotate all credentials or IdentityServer registrations; a single-service deployment can create generation skew. | Merged and CI-tested; legacy independent deployment is frozen and rotation is full-stack/run-scoped. Live evidence pending. |
| P0-R15 | Medium | The preflight claims bounded limits and one replica without validating every exact configured quota or the runtime Hub process count. | Merged and CI-tested verifier implementation covers a separate per-principal pending-RPC ceiling and one-Hub runtime inventory. Live process inventory remains pending. |
| P0-R16 | High | Exact image identity is not bound to the reviewed source or trusted builder. | Merged and CI-tested with signed provenance binding digest, commit, workflow/builder, Dockerfile, and base images. Live attestations pending. |
| P0-R17 | High | Rotation does not define activation order, dual-generation validation, old-generation revocation, or convergence across staged restarts and HTTP validators. | Merged and failure-tested in committed CI. Live convergence/revocation evidence pending. |
| P0-R18 | Critical/High | Workflow-controlled root execution, mutable recovery components, circular first-run bootstrap, activation crash windows, stale synthetic leases, replaceable recovery locks, and non-durable LKG publication could bypass or strand fail-closed recovery. | Merged and CI-tested with automation that requires an operator-reviewed root-only staging bundle; descriptor-relative complete reads and atomic installation; a fixed root-owned, inode-stable deployment lock shared by root activation and recovery; bounded Docker escalation; a fixed operator-installed root helper/launcher/lease manager; narrow sudoers commands; root-only quarantine; fresh-lease first rollout; supervised synthetic heartbeats; an always-active watchdog timer; crash-durable sealing/pointer publication; and privileged adversarial recovery tests. The actual operator staging review, bootstrap, and live evidence remain pending. |
| P0-R19 | High | Canary health used a stale server schema and generic service health, so real payloads failed validation while transient client send/receive failures could disappear between samples. | Merged and CI-tested with the exact server/client snapshot schemas, shared loopback-only client transport health, process-lifetime fault/disconnect/reconnect watermarks, and strict observation tests. Live canary evidence remains pending. |
| P0-R20 | High | A nonparticipant stream-close frame reached the route-removal path before cleanup authorization, allowing an unrelated connection to remove an active logical stream. | Implemented by authorizing stream participation before routing or removal, with an order-stable regression test. |
| P0-R21 | High | The synthetic token-refresh hook expected a generic `Result` envelope, but generated successful IdentityServer HTTP adapters return the exact bare DTO. Every live token refresh would fail schema validation before authenticated WSS synthetics ran. | Fixed locally with exact bare service/user DTO validation, semantic validation of token type/lifetime/refresh/session fields, explicit legacy-envelope rejection, generated response population, and generated-adapter plus integration coverage. Review, committed Linux CI, merge, and live evidence remain pending. |
| P0-R22 | High | The marker-absence contract unconditionally required proxy log paths even though xeon-dev publishes TLS Kestrel directly. It had no honest not-applicable proof and could encourage fabricated empty proxy evidence. | Fixed locally by making only `direct-kestrel` promotion-eligible, requiring proxy paths to be absent, rejecting reruns in favor of a fresh dispatch, cross-binding exact Hub TLS publication plus public-DNS/active-host-interface identity, and sealing an explicit no-intermediary operator attestation with stable actor ID, matching triggering actor, first attempt, run, commit, hostname, and port. Recovery and every no-lease/no-LKG watchdog path fail closed on mode drift. This assumes the protected self-hosted runner is trusted; a stronger threat model requires GitHub OIDC-signed authorization. The utility `logs` mode cannot qualify until its synthetic traverses the same proxy and its retained-store inventory is sealed. Seq/trace retained-store proof remains mandatory. Review, committed Linux CI, merge, and live evidence remain pending. |

## Phase 0 - Immediate Containment

### Objective

Remove practical exploit paths and deployment hazards without waiting for architectural work.

### Implementation Status - 2026-07-13

**Disposition:** `Contained`, not `Verified`.

| Area | Status | Evidence or remaining gate |
|---|---|---|
| Registration identity enforcement | Implemented | Non-Development resolves only to `Enforce`; all reserved service names are covered by negative registration tests. |
| Emergency migration mapping | Implemented | Disabled by default; exact authenticated-service/client mapping; logged; expired entries fail startup; lifetime is capped at seven days. |
| Frame and resource containment | Implemented | Protocol/client/server frame defaults are 8 MiB; global and per-principal RPC/connection/stream/media/subscription limits are tested; durable topic cardinality is atomically capped. |
| Topic and discovery authorization | Implemented | Unknown namespaces default-deny; Communications grammar is bounded; local discovery uses the HTTP service/admin policy; authorization failures are contained per frame. |
| Socket lifetime and shutdown | Implemented | Token expiration and absolute lifetime cancel connections; transport close and lifecycle callbacks are bounded; server disposal cancels active loops. |
| Media quarantine | Implemented | Hub Media is disabled in every checked-in environment and one Hub replica is declared. |
| Credential redaction | Partially verified | First Hub middleware removes query tokens from application request/telemetry surfaces. Server pre-middleware and Seq evidence remain required. Proxy/ingress retained-store evidence is required only when root-verified publication topology includes those layers; a root-sealed direct-Kestrel topology requires an explicit not-applicable receipt instead. |
| TLS deployment boundary | Implemented; rollout pending | Direct Kestrel HTTPS, TLS-only publication, WSS clients, separate Hub/IdentityServer key boundaries, fatal CA trust, runtime listener/plaintext rejection, public DNS/TLS, and resolved mount isolation are automated. The contained image is not yet deployed. |
| Secret exposure response | Partially verified | Current Docker/Seq logs and the Seq volume were preserved and hashed before changes. Precise retained-source searches found no credential exposure, but earlier exposure remains unexcluded. Coordinated rotation is automated but has not run live. |
| Staging acceptance | Automated; retry corrections pending review/CI | Authenticated registration/RPC/pub-sub/durable/expiry, marker searches, observation, rotation, rollback, qualification, and forced recovery are implemented. Protected xeon-dev inputs are provisioned; the P0-R21/P0-R22 corrections and live evidence remain required. |
| Automated evidence | Merged Linux baseline passed; retry corrections have local evidence only | Committed Ubuntu run `29229428707` built the Release solution with 0 errors and built the Bolt Hub container. `Bolt.Tests` passed 303/303 with Redis mandatory and zero skips; IdentityServer passed 21/21; Core passed 195/195; `Bolt.Phase0Synthetics.Tests` passed 40/40 with zero skips; all 19 privileged Python suites passed 370 tests with zero skips. The current local tree passes 23/23 IdentityServer unit tests and the Phase 0 Python contracts with platform-specific Windows skips; all Phase 0 Python files compile, workflows parse, and shell files pass syntax checks. The updated CI runs the promotion-eligible direct-Kestrel wrapper contract plus seven negative scenarios. A fresh committed Ubuntu gate is still required. |
| Independent verification | Pre-final reviews completed; final-tree approval and live gates pending | Targeted security/correctness reviews found and drove fixes for health exposure, scope-shape bypass, send-loop failure, durable ACK, JWT expiry, large-RPC quota cleanup, recovery trust/durability, client failure watermarks, stream-close authorization, privileged bootstrap completeness, and lock-inode stability. PR #352 merged without a formal GitHub review, so an independent review bound to the exact final tested tree remains required. Operator bootstrap and live staged evidence also remain required. Deferred Phase 3/4 client findings remain open. |

Deployment procedure and required evidence are defined in
`docs/solutions/workflow-issues/bolt-phase0-deployment-gate-2026-07-13.md`.

### Deliverables

1. Set `RegistrationIdentityBindingMode=Enforce` in every non-Development Hub environment and make startup fail when it resolves to `Off` or `Audit` outside Development.
2. Reject reserved service IDs and names unless the authenticated principal has the service scope and exact matching service identity claim.
3. Add an emergency exact-service allowlist only for migration recovery; entries must be explicit, logged, expiring, and disabled by default. Never roll back to Audit.
4. Require TLS at the deployment boundary, remove direct plaintext Hub exposure, and reject `ws://` configuration outside Development.
5. Lower the provisional global frame ceiling from 100 MB based on current production payload telemetry. Require existing large-RPC streaming above that ceiling.
6. Add a temporary connection, pending-RPC, stream, and media-config quota per principal while byte-aware governance is built in Phase 4.
7. Introduce a Bolt Media kill switch and keep high-level browser media disabled until Phases 7 and 8 pass.
8. Enforce a single Bolt Hub replica in deployment configuration until Phase 6 completes.
9. Align Staging Kestrel, published, and health-check ports.
10. Add structured security logs and counters for rejected registration identity, oversized frame, quota rejection, plaintext configuration, and disabled media access.
11. Default-deny unknown topic namespaces, cap subscriber cardinality, and restrict Bolt-local discovery to the same service/admin policy used by HTTP.
12. Catch authorization/tenant failures per frame and return denial without closing an unrelated shared service connection.
13. Enforce a temporary absolute socket lifetime from token expiration while Phase 1 reauthentication/revocation is built.
14. Redact WebSocket query credentials from every current log path and withdraw unsupported media, transport, zero-GC, and universal performance claims.
15. Preserve relevant security logs and rotate JWT, Bolt, and service client secrets if prior plaintext or route-takeover exposure cannot be excluded.

### Acceptance Gate

- A normal authenticated user cannot register, join, or receive traffic from any reserved service pool.
- A mismatched service token is rejected before registration acknowledgement.
- Non-Development startup fails for Audit/Off binding or plaintext server/client configuration.
- Oversized frames and quota excess close or reject only the offending operation/connection without unbounded allocation.
- Prohibited topic/discovery operations create no Hub, Redis, or database state.
- An authorization failure rejects one frame without closing the shared connection.
- Expired sockets close within 60 seconds.
- Access tokens do not appear in application, Seq, or telemetry logs, or in proxy/ingress retained stores when root-verified publication topology includes those layers; direct publication has a root-sealed not-applicable receipt.
- Staging container starts and passes `/health/live` and `/health/ready` on the published port.
- Approved repository mapping, registry confirmation, exact runtime digests, actual listeners, peer plaintext rejection, resolved key mounts, real DNS/TLS, every quota, and exactly one running Hub are captured.
- Deployment manifest and runtime evidence prove one Hub replica and Bolt Media disabled.
- Offline durable replay/ack/no-redelivery and Redis interruption synthetics pass before broad rollout.
- Failure injection executes the security-qualified full-stack rollback with the current credential generation or stops Bolt traffic.
- Rotation tests prove `G+1` pre-staging, canary activation, mixed-generation validation, full runtime convergence, old-token expiry, `G` revocation, rejection of old tokens/client secrets, and unaffected required HTTP health throughout the bounded window.

### Rollback

Security controls are not rolled back. If a service cannot connect, fix its identity claims or use the expiring exact-service migration allowlist. Keep the Hub unavailable rather than reopening Audit mode.

## Phase 1 - Transport Identity and Secure Channel

### Objective

Replace locally minted shared-HMAC handshake tokens with centralized, service-bound transport identity and end-to-end secure transport.

### Architecture Decision

Review and approve the draft [centralized asymmetric transport identity ADR](../architecture-patterns/bolt-phase1-centralized-asymmetric-transport-identity.md) before implementation. The design must break the current circular dependency: obtaining the token required to connect to Bolt cannot itself require a Bolt call. The ADR remains research-only until Phase 0 is live-verified.

### Deliverables

1. Add a dedicated `IBoltTransportTokenProvider` separate from RPC `IServiceTokenProvider`.
2. Obtain short-lived Bolt transport tokens from IdentityServer over HTTPS or an approved workload-identity channel, never through Bolt.
3. Issue asymmetric tokens with a Bolt Hub audience, explicit transport-connect scope, exact service identity, `iat`, `nbf`, `exp`, and unique token ID.
4. Let Bolt Hub validate IdentityServer public keys/JWKS; remove caller access to any signing key accepted by the Hub.
5. Define IdentityServer's bootstrap explicitly: local asymmetric self-issuance or mTLS/workload identity, without a shared application HMAC secret.
6. Split user and service WebSocket authentication schemes and policies.
7. Derive the routable service/user identity server-side from validated claims. Treat client-supplied ID/name as display or compatibility metadata only.
8. Remove `GenerateBoltServiceAccessToken` and stop distributing the Hub JWT signing secret to application services.
9. Refresh/reconnect before token expiry with jittered single-flight renewal; close connections at expiry or revocation.
10. Ensure query-string access tokens are redacted from ASP.NET, Seq, and OpenTelemetry logs and
    from every proxy/ingress layer present in verified publication topology.
11. Rotate and revoke the old shared HMAC secret after all clients migrate.
12. Issue transport tokens with a maximum 60-second lifetime and at most 5 seconds of clock skew. Track `jti`, `kid`, principal/service identity, issue time, and expiry on each connection without logging bearer values.
13. Publish a signed IdentityServer revocation epoch/status feed over HTTPS for disabled principals, services, token IDs, and compromised signing keys. Hub polling or push-with-poll-fallback must observe changes within 15 seconds and close matching sockets; expiry remains the fail-safe bound.
14. Implement JWKS lifecycle with explicit `kid`, algorithm allowlisting, bounded cache age, refresh-on-unknown-`kid` once, overlap for at least twice the maximum token lifetime, scheduled rollover, emergency key removal, and fail-closed behavior when a key cannot be validated.
15. Prove issuance-key activation, validator pre-staging, overlap, cache refresh, old-key retirement, compromise revocation, and IdentityServer outage behavior without accepting a caller-held signing key.

### Acceptance Gate

- A token self-signed by Service A while claiming Service B is rejected.
- A normal user token cannot register a service identity or receive service RPCs.
- Expired, revoked, wrong-audience, wrong-scope, missing-expiration, and mismatched-identity tokens are rejected.
- An already connected socket closes or reauthenticates at token expiry, and disabling/revoking its principal terminates or reauthenticates it within 60 seconds.
- IdentityServer and all other services can cold-start without a Bolt/token circular dependency.
- No service client secret appears in Bolt frames or logs during transport authentication.
- WSS/mTLS handshake, certificate rotation, token renewal, and IdentityServer temporary outage tests pass.
- Scheduled signing-key rollover produces no authentication outage; an unknown or removed `kid`, algorithm substitution, stale JWKS beyond its bound, or compromised key is rejected and matching sockets close within 60 seconds.
- Disabling a user/service or revoking a token increments the signed revocation state, reaches every Hub within 15 seconds, and closes matching sockets within 60 seconds even when push delivery is unavailable.

### Compatibility and Rollout

Use a short shadow-validation window in which new tokens are validated and measured before enforcement. Do not accept legacy identity mismatches. Remove the legacy token path in the same release train rather than leaving permanent dual authentication.

## Phase 2 - Measurement and Observability Foundation

### Objective

Create trustworthy measurements before lifecycle, memory, and wire optimizations change behavior.

### Deliverables

1. Correct BenchmarkDotNet operation accounting with one logical operation per invocation or accurate `OperationsPerInvoke` metadata.
2. Stop presenting batch-amortized wall time as individual request latency.
3. Compare equivalent raw-to-raw and typed-to-typed work, including response validation and deserialization.
4. Fail benchmark iterations on non-success status or wrong response payload.
5. Sweep equal connection/channel counts, then separately compare each transport's documented best configuration.
6. Add tuned gRPC channel reuse, multiple HTTP/2 connection, flow-control, unary, server-streaming, and bidirectional-streaming cases.
7. Add secure multi-process and multi-host runs with controlled RTT, bandwidth, and loss; retain localhost microbenchmarks only as a developer fast path.
8. Record commit, runtime, GC mode, CPU, OS, transport security, payload, concurrency, channel count, and benchmark configuration with every artifact.
9. Freeze and publish the tuned-gRPC comparison contract: exact package/runtime versions, protobuf/serializer work, channel reuse, HTTP/2 connection and stream windows, connection counts, TLS/auth, compression, retries, GC mode, topology, and resource limits. Run equal-resource sweeps separately from each transport's documented best configuration.
10. Collect request-level HDR p50/p95/p99/p99.9, error rate, queue time, CPU, context switches, working set, allocation bytes, pool retention, and GC pause time for every participating client, Hub, and backend process.
11. Add a Bolt `Meter` and `ActivitySource` for pending calls, queue bytes, active sends, send failures, route misses, rejects, stream resets, media drops, replay backlog, and command latency.
12. Establish a dedicated stable performance runner and a nonblocking baseline dashboard before enabling CI regression gates.
13. Predeclare primary benchmark cells, non-inferiority/superiority formulas, exclusion rules, and multiple-comparison treatment; publish every valid cell, including cells where Bolt loses.

### Acceptance Gate

- A known batch benchmark reports the mathematically correct logical operation count.
- Raw and typed comparison cases perform equivalent work and validate results.
- Bolt and gRPC run with identical TLS/topology/payload/concurrency conditions.
- Results report 95% confidence intervals and meet the Quantitative Test Envelope reproducibility threshold across repeated launches.
- At least ten independent dedicated-runner launches produce throughput coefficient of variation at or below 5% before a regression threshold is enabled.
- Every result artifact identifies code commit and machine/runtime configuration.
- Every result includes aggregate client, Hub, and backend CPU/allocation/working-set accounting and applies the claim definitions in the Quantitative Test Envelope without selectively omitting losing cells.
- Metrics tests prove bounded cardinality and correct counter balance on success, timeout, cancellation, disconnect, and rejection.

### Initial Performance Targets

These are engineering targets, not claims, until Phase 9:

- No errors or hangs at the declared saturation point.
- Bolt p99 no worse than tuned gRPC for 64 B to 64 KB unary RPC at controlled 1 ms and 10 ms RTT.
- Bolt throughput at least equal to tuned gRPC under equal security and connection limits.
- Bolt allocated bytes per successful RPC at most 60% of tuned gRPC for the core small-payload workload.
- Stretch target: at least 20% better p50/p99 or throughput while preserving the memory target.

## Phase 3 - RPC, Connection, and Stream Lifecycle Correctness

### Objective

Make completion ownership, send semantics, connection affinity, cancellation, reconnection, and logical-stream isolation deterministic.

### Deliverables

1. Redesign pending RPC ownership so the exact dictionary registration is removed before a completion object can return to its pool.
2. Add generation/identity checks to pooled completions, or temporarily remove pooling until deterministic race tests pass.
3. Split enqueue semantics from transport-send semantics. RPC uses a send completion that observes transport failure; explicitly best-effort push APIs may retain enqueue-only semantics.
4. Propagate caller cancellation immediately to the pending RPC and queued send.
5. On transport-send error, fail affected pending work, retire the connection, and trigger bounded reconnect instead of swallowing the exception.
6. Preserve inbound physical-connection affinity for all small and streamed large-RPC responses.
7. Replace receive-loop waits on logical stream consumers with nonblocking demultiplexing and per-stream overflow/reset behavior.
8. Reject unknown stream commands and cap active streams per connection/principal.
9. Add bounded request/push/stream dispatch schedulers with explicit overload responses.
10. Add bounded per-principal media-config dispatch, controller/task limits, and deterministic cancellation/disposal when a config replaces an existing stream/controller.
11. Add one end-to-end connection/register/ack deadline and dispose all failed transport resources.
12. Implement a single-flight connection state machine for connect, reconnect, scale-up, scale-down, dispose, and pool restoration.
13. Replace `ConcurrentBag` recipient routing with an atomic immutable/snapshot structure that supports O(1) round-robin without losing concurrent adds/removes.
14. Complete streams, subscriptions, media controllers, and pending sends exactly once when their owning connection fails.
15. On local deadline/cancellation, atomically remove the exact pending registration, reject late/duplicate responses, and release all local queue/pending budgets even though Protocol v1 cannot yet cancel remote work.
16. Add a checked-in receiver-side maximum handler execution deadline with bounded per-command overrides. Link it to handler/downstream cancellation and release dispatch/byte budgets when it expires. This wire-compatible safety ceiling remains authoritative until Phase 5 can propagate the caller's stricter deadline.

### Acceptance Gate

- Deterministic ABA tests cover timeout, pool return, re-rent, late response, and duplicate response without cross-completion.
- Blocked enqueue, blocked transport send, caller cancellation, send failure, and response timeout each terminate within their declared deadline and return the correct exception.
- Large request and response tests pass with at least two connections on caller and responder.
- An unread/overflowed logical stream is reset without delaying an unrelated RPC on the same physical connection.
- Reconnect/scale/dispose stress leaves no resurrected connections, pending work, or uncompleted channels.
- Recipient registration/removal concurrency tests lose no live connection and preserve fair selection.
- A media-config storm cannot exceed configured controller/task limits; replacements and disconnect return active media controller/task counts to zero.
- Deadline/cancellation/response race tests prove the local caller completes once, late responses cannot complete reused state, and every local budget is released. Caller-specific remote cancellation remains deferred to Phase 5; the fixed receiver execution ceiling below is the interim bound.
- A handler that ignores or exceeds the configured receiver deadline is canceled at the boundary, loses its dispatch/byte budget, cannot publish a late response, and does not delay unrelated work. Downstream handlers that honor cancellation terminate within the configured deadline plus 100 ms.

## Phase 4 - Resource Governance and Memory Ownership

### Objective

Make memory and work bounded by bytes, the Phase 3 receiver execution ceiling, and principal while reducing copies and eliminating finalizer-dependent hot-path ownership.

### Deliverables

1. Add byte budgets in addition to count limits for connection queues, principal queues, Hub totals, streams, pending RPCs, pub/sub, and media fanout.
2. Add protocol/frame-type ceilings for control, unary RPC, stream chunk, pub/sub, media config, and media frame data.
3. Reserve budget before renting/copying and release it in the same `finally` that returns ownership.
4. Replace custom frame callbacks with an explicit scoped or reference-counted `IMemoryOwner<byte>` contract.
5. Return Event and Bolt Media input buffers deterministically and remove redundant payload copies where lifetime permits.
6. Introduce a ref-counted frame owner for Hub fanout so one validated frame can be shared safely across recipients.
7. Start receive buffers small and return fragmented-message assembly buffers immediately after dispatch.
8. Stream rather than aggregate payloads above the checked-in wire-compatible unary threshold. Phase 5 replaces this fixed compatibility threshold with a negotiated value.
9. Remove finalizable `PooledMemoryOwner` from normal RPC/stream paths and expose deterministic disposal to consumers.
10. Measure `System.IO.Pipelines`, scatter/gather, and transport-native batching before selecting the receive/send representation.
11. Remove per-send timeout/linked-CTS allocations from the steady-state path using shared deadlines or a measured equivalent, without weakening cancellation.
12. Enforce browser frame and allocation limits equivalent to .NET.

### Acceptance Gate

- Slow-reader, oversized-frame, fanout, pending-RPC, and stream-flood tests stay within the configured aggregate byte budget plus a documented fixed overhead.
- A 60-minute overload test includes an attempted 100 MB frame, maximum supported fanout, a non-reading peer, unknown streams, and pending-RPC pressure without exceeding configured budgets plus one in-flight chunk per connection.
- During that overload, an unrelated RPC remains live and its p99 stays below twice its unloaded baseline.
- Working set and retained pool bytes satisfy the Quantitative Test Envelope after overload and pool trim; no connection retains its largest fragmented frame.
- Retained pool bytes return to baseline plus fixed receive buffers within 60 seconds after the overload drains.
- A counting test pool reports exactly one return for every rent across success, reject, timeout, cancellation, handler exception, and disconnect.
- No LOH allocation occurs for frames below the configured streaming threshold.
- Copy bytes per unary RPC and per media recipient are measured and reduced from the Phase 2 baseline.
- Performance regressions above 5% in a guarded metric require an explicit security/correctness waiver and follow-up issue.

## Phase 5 - Versioned Protocol and Cross-Runtime Parity

### Objective

Create a rolling-upgrade-safe wire contract that detects capability mismatch and hash collisions without sacrificing compact steady-state framing.

### Deliverables

1. Write an ADR for Bolt protocol versioning, compatibility duration, deprecation, and downgrade policy.
2. Add handshake negotiation for protocol version, frame limits, command/service addressing, streaming, compression, media, encryption, and transport capabilities.
3. Replace bare cross-peer 32-bit command/service hashes with collision-resistant stable identities during negotiation.
4. Benchmark a negotiated compact session command/service ID table so steady-state frames remain small without trusting an unverified 32-bit global hash.
5. Bind service routing to the authenticated server-side identity rather than a client-supplied route hash.
6. Centralize writer/reader constants and validation so a writer cannot produce a frame rejected by the matching reader.
7. Enforce exact lengths, valid discriminators, bounded NACK counts, valid UTF-8/UUID data, and explicit trailing-byte policy.
8. Use JavaScript `bigint` or validated string encoding for 64-bit values; reject invalid UUID/hex input.
9. Generate shared .NET/TypeScript golden vectors for every frame and capability combination.
10. Add mutation, truncation, boundary, collision, and fuzz tests for .NET and browser decoders.
11. Support a time-bounded v1/v2 rolling deployment, emit downgrade telemetry, then disable v1 outside an explicit compatibility window.
12. Make WebTransport disposal close the underlying session and prove closure/cancellation in transport lifecycle tests.
13. Correct or remove the inaccurate claim that pooled arrays are not GC-tracked and align all ownership comments with the implemented deterministic lifetime contract.
14. Add negotiated connection-level and stream-level credit windows, half-close, cancellation, and reset semantics. A sender may retain at most the advertised window plus one negotiated chunk for a slow reader.
15. Add negotiated absolute RPC deadlines and an idempotent cancellation frame keyed by request identity. The Hub and receiver reject expired work before dispatch and cancel the exact active handler/downstream token when cancellation wins the race.
16. Negotiate the unary/streaming threshold and reject peers whose required frame, window, deadline, or cancellation capabilities are incompatible.

### Acceptance Gate

- Known FNV collision strings remain distinct end to end and cannot invoke the wrong handler or route.
- v1/v2, v2/v1, and v2/v2 handshakes follow the documented compatibility policy.
- Unsupported required capabilities fail during handshake, not after business traffic starts.
- Every golden vector round-trips identically in .NET and TypeScript.
- At least 100,000 generated boundary/mutation cases complete without .NET/TypeScript reader divergence.
- Fuzzing produces no crash, unbounded allocation, parser disagreement, or accepted malformed length.
- Negotiated steady-state header/copy cost is measured against v1 and justified by the Phase 2 harness.
- WebTransport disposal closes its session, releases pending receive/send work, and leaves no active transport task.
- Ownership documentation matches tests and contains no claim that pooled arrays bypass GC tracking.
- Slow-reader tests prove outstanding sender bytes never exceed the negotiated connection/stream window plus one chunk, and resetting one stream does not stall another.
- Deadline propagation tests cover expiry before Hub routing, before handler dispatch, during handler/downstream work, cancellation/response races, duplicate cancellation, disconnect, and clock skew. Work and budgets terminate within the declared bound and late responses are ignored.

## Phase 6 - Hub Pub/Sub, Discovery, and Horizontal Scaling

### Objective

Make Hub-owned state authorized, bounded, efficient, tenant-correct, and safe across multiple Hub instances.

### Deliverables

1. Default-deny unknown topic namespaces and define strict topic/subscriber grammar, size, lease, and per-principal cardinality limits.
2. Replace per-frame Communications database authorization with a short-lived signed topic capability issued by the owning module and verified offline by the Hub.
3. Bind capability claims to actor, tenant, topic pattern, operations, subscriber ID, and expiration.
4. Contain authorization failure to the offending frame; never close a shared service connection for a tenant/filter exception.
5. Remove per-ack credential queries and cache only validated capability state until expiration/revocation.
6. Persist service discovery only for claim-bound service advertisements, not ordinary user/media clients.
7. Correct configured manifest identities, evaluate `MinVersion` with documented SemVer rules, and normalize identity comparison consistently in memory and PostgreSQL.
8. Add expiring presence leases, heartbeat/renewal, disconnect retry/reconciliation, retention, pagination, and an immutable registry snapshot.
9. Bound Redis subscriber membership, deferred replay, topic/hash registries, publish locks, and replay work by bytes/time/cardinality.
10. Write an ADR comparing single-replica routing, sticky sharding, backplane, and an authenticated inter-Hub transport using measured latency and failure behavior.
11. Implement instance-aware routing/presence and cross-node transient/durable pub/sub before permitting more than one replica.
12. Add PostgreSQL- and Redis-backed integration tests instead of relying only on EF InMemory.

### Acceptance Gate

- Unknown namespace, malformed subscriber, cross-tenant capability, expired capability, and quota-excess operations are denied without database access on the hot path.
- One million synthetic offline/churn records remain paginated/retained, and the 10,000/100,000-record benchmark satisfies the registry-scaling threshold in the Quantitative Test Envelope.
- Presence converges after startup, disconnect, Hub crash, database outage, and lease expiry.
- Two real Hub processes route RPC, streams, transient pub/sub, durable live/replay, call signaling, and disconnect cleanup correctly across nodes. Media frames remain disabled until Phase 7 and require a separate multi-Hub media gate there.
- Redis/database work per publish/ack remains bounded and is asserted by tests/metrics.
- Multi-replica deployment remains blocked until every two-Hub test passes.

## Phase 7 - Bolt Media Core Correctness and Security

### Dependencies

Phases 1 through 6 must be complete. Media signaling and encryption rely on authenticated identity, trustworthy measurement, deterministic ownership, bounded transport behavior, versioned capabilities, and correct multi-Hub routing. The crypto/signaling ADR and threat model require an independent security review before implementation begins, followed by a separate implementation/evidence review at the phase gate.

### Objective

Make two-peer and group media function end to end without plaintext downgrade, sequence amplification, FEC leaks, or browser wiring gaps.

### Deliverables

1. Use wrap-aware bounded sequence windows; reject implausible jumps before iterating or allocating.
2. Cap NACK entries to the negotiated small limit, deduplicate them, and enforce retransmit request/byte budgets.
3. Separate bandwidth probes from normal media sequence and decode paths.
4. Disable FEC/NACK on reliable ordered transports unless measurement proves a valid use; negotiate them only for transports where loss recovery applies.
5. Correct FEC group mapping, parity validation, expiration, recovery order, encryption handling, and memory cleanup.
6. Add registered outgoing-stream and remote-stream-added APIs; connect browser encoders to registered send streams and remote streams to decoders/playback.
7. Define a versioned signaling envelope carrying authenticated caller identity, media intent, encryption requirement, directional codec capabilities, timestamps, and decoder configuration.
8. Make encryption fail closed: no media leaves or reaches a decoder until required key confirmation succeeds.
9. Sign and bind ephemeral keys to authenticated call identity, transcript, participants, role, and protocol version; add replay protection and mutual key confirmation.
10. Isolate asynchronous crypto state per call/sender and remove synchronous waits on JavaScript promises.
11. Implement membership snapshots, join/leave acknowledgement, config replay, removal notification, and epoch-based group/per-sender key rotation.
12. Feed only validated encoded payloads to media processors, honor `Accepts`, exclude FEC/probes, and finalize processors exactly once on every end/disconnect path.
13. Apply VAD/PLC/audio processing only to decoded PCM, never encoded Opus or other codec bytes.
14. Normalize timestamp clock domains, preserve codec metadata, advertise the selected codec, and make Hold stop capture, send, routing, and playback as specified.
15. Keep the media feature disabled until the acceptance gate and independent crypto review pass.
16. Define checked-in numeric limits before implementation for sequence acceptance windows, NACK entries/rate/retransmit bytes, retained FEC groups/bytes/expiry, jitter/replay retention, per-call/per-sender tasks, and maximum processing time. Boundary tests must use those exact values; implicit or effectively unbounded defaults are prohibited.
17. Define identity-key trust, signing-key rotation/revocation, AEAD algorithm and nonce construction, authenticated-data fields, replay windows, epoch transitions, key confirmation, participant removal, and deterministic key destruction in the reviewed crypto ADR.
18. Run multi-Hub media routing only after single-Hub two-peer and group correctness pass; prove sender/recipient ownership, encrypted forwarding, reconnect, rekey, and cleanup across two real Hub processes before declaring H20's media portion fixed.

### Acceptance Gate

- A real two-browser call runs for at least 60 seconds and covers initiate, answer-side setup, audio/video encode, Hub route, remote decode/playback, hold/resume, end, reconnect, and cleanup.
- The two-browser test uses independent browser processes and proves bidirectional encoded capture, Hub routing, remote decode, and actual rendered/playable output rather than only signaling or compile success.
- Hold reduces outbound media to zero within 250 ms and Resume restarts only the negotiated tracks.
- Large/wrapped sequence jumps and duplicate maximum-size NACK input remain bounded and do not block the client or Hub.
- Loss/reorder tests demonstrate correct FEC/NACK behavior and group expiry with and without encryption.
- An encryption-required call emits zero plaintext media before/after key setup, rejects substitution/replay/downgrade, and isolates concurrent calls.
- Three-party join, removal, config replay, rekey, and encrypted media converge for every participant; a removed or not-yet-joined participant cannot decrypt the active epoch.
- Processor/recording tests receive valid payloads only and close all files/state on normal end and disconnect.
- Exact-boundary tests prove the configured sequence, NACK, retransmit, FEC, jitter/replay, task, byte, and processing-time limits. Reliable ordered transports negotiate FEC/NACK off.
- The initial 8-video/16-audio group envelope passes join, leave, reconnect, and rekey while Hub media-route p99 is at most 5 ms on the dedicated same-host runner, sustained CPU remains below 70% of allocated cores, and memory recovery satisfies the Quantitative Test Envelope after a 60-minute soak.
- After normal end, failed negotiation, browser close, peer disconnect, and Hub restart, active media tasks, retained media/FEC/jitter buffers, processors, recorders, call memberships, and call keys return to zero or the documented fixed baseline.
- Two real Hub processes pass encrypted media forwarding, participant movement/reconnect, rekey, and cleanup with no plaintext downgrade before the media portion of horizontal scaling is enabled.
- A separate sub-agent plus a qualified human reviewer approve the crypto/signaling design and test evidence.

## Phase 8 - Media Adaptation and Production Transports

### Dependencies

Phases 2, 4, 5, and 7 must be `Verified`. No adaptive or production transport path may bypass the Phase 4 budgets, Phase 5 negotiation contract, or Phase 7 encryption and cleanup invariants.

### Objective

Finish the adaptive media and transport paths only after core media is correct and measurable.

### Deliverables

1. Attach congestion controllers to sender-owned streams and normalize all feedback clock domains.
2. Integrate bounded jitter/replay buffers into receive/playout, including late, reordered, retransmitted, and wrapped packets.
3. Wire probe feedback, simulcast layer metadata, receiver selection, keyframe requests, and bitrate changes end to end.
4. Define a supported media transport matrix and remove or clearly disable unimplemented claims.
5. Implement both directions of any selected QUIC/WebTransport datagram path, including fallback, limits, loss, and reconnect.
6. Do not ship the current hand-rolled direct WebSocket P2P path. Use an established ICE/STUN/TURN connectivity implementation if P2P is required, while retaining Bolt framing only where it adds value.
7. Add browser compatibility, device, background-tab, permission, network-change, and fallback tests.
8. Add per-call/participant media benchmarks for frame size, group size, fanout copies, p95/p99 route latency, loss recovery, CPU, working set, and GC.

### Acceptance Gate

- ABR uses one clock domain and satisfies the numeric convergence/recovery thresholds in the Quantitative Test Envelope without oscillation.
- Jitter buffer meets the numeric p99 playout threshold, recovers the declared loss/reorder profile, and releases all retained frames.
- Simulcast recipients receive only the selected negotiated layer and complete keyframe/layer change within 2 seconds.
- Datagram/fallback tests pass across supported browsers and server transports, or the capability remains disabled and undocumented as available.
- P2P, when enabled, passes authenticated peer, NAT traversal, relay fallback, certificate/key, SSRF, and teardown tests using the selected established engine.
- Media load stays inside Phase 4 byte budgets for the 8-video/16-audio participant envelope.
- A 60-minute cross-host media soak at the declared group envelope sustains CPU below 70% of allocated cores, satisfies Phase 7 route-latency and memory-recovery gates, and leaves no media tasks or retained buffers after teardown.
- Capability documentation advertises only browser/transport combinations that pass cross-host loss, reorder, reconnect, network-change, and fallback tests; all other combinations remain disabled and undocumented as available.

## Phase 9 - Performance Optimization and Release Certification

### Objective

Optimize only measured bottlenecks, then produce defensible workload-specific performance and memory claims.

### Optimization Loop

1. Profile CPU, allocation, copy bytes, syscalls, locks, scheduler delay, queue delay, and GC under the Phase 2 workload matrix.
2. Change one hot-path mechanism at a time: batching, buffer ownership, parsing, serialization, connection selection, timer/cancellation strategy, or compact negotiated IDs.
3. Run correctness, adversarial, soak, and failure tests before accepting a benchmark gain.
4. Compare confidence intervals on the dedicated runner and reject gains that disappear outside localhost or increase p99/error/memory risk.
5. Preserve a readable reference implementation or targeted comments where an optimization makes ownership non-obvious.
6. Require a profile-targeted optimization to improve its target by at least 10% with a 95% confidence interval excluding zero and no greater than 5% regression in gated p99, throughput, CPU, errors, or memory.

### Certification Matrix

- Payloads: 0 B, 64 B, 1 KB, 16 KB, 64 KB, 1 MB, streamed 10/100 MB.
- Concurrency: 1, 8, 32, 64, 256 and declared saturation.
- Topology: in-process baseline, same host/process-separated, cross-host.
- RTT: local plus controlled 1, 10, 50, and 100 ms profiles.
- Bandwidth/loss: controlled 10/100/1,000 Mbps and 0%, 0.1%, 1%, and 5% loss profiles where the transport semantics apply.
- Load model: both closed-loop and open-loop offered load with coordinated-omission-corrected request histograms.
- Lifecycle: cold start, warm steady state, reconnect, rolling restart, and 1/10/100/1,000/10,000-client sweeps.
- Security: TLS 1.3 and service mTLS/central token policy equivalent across transports.
- Workloads: raw unary, typed unary, server/client/bidirectional stream, transient pub/sub, durable replay/ack, fanout, reconnect/churn, and media.
- Failure injection: slow/non-reading consumers, abrupt peer/Hub kill, network partition, token expiry/revocation, Redis/database outage, and recovery.
- Competitors: tuned gRPC unary/streaming, SignalR where semantically comparable, and approved HTTP/2 or HTTP/3 baselines.
- Metrics: throughput, p50/p95/p99/p99.9, errors/timeouts, queue time, CPU, context switches, syscalls, copy bytes, allocated bytes/op, working set, retained pool bytes, and GC pauses.

### Release Gate

- All Critical and High findings are `Verified`; Medium findings are `Verified` or explicitly accepted by ADR with owner and expiry.
- Zero correctness errors, cross-request completions, silent send losses, unbounded growth, or plaintext downgrade in saturation and soak tests.
- Bolt meets the Phase 2 minimum targets across the Quantitative Test Envelope.
- Any "faster than" statement names payload, concurrency, topology, security, compared configuration, hardware, commit, and confidence interval.
- Documentation capability tables and examples compile/run against the released API.
- Canary deployment shows no security rejection anomaly, queue growth, pool retention, reconnect storm, or latency regression before broad rollout.

## Independent Sub-Agent Verification Gate

Every implementation phase uses a verifier that did not author that phase's code.

### Required Inputs

- Source audit and this plan.
- Exact finding IDs assigned to the phase.
- Phase PR diff and list of touched files.
- Test, fuzz, benchmark, container, and deployment evidence.
- Before/after metrics and any approved waivers.
- Updated finding status/evidence table.

### Required Verifier Output

1. Findings fully fixed, partially fixed, missed, or regressed.
2. Security/correctness/performance risks introduced by the implementation.
3. Missing negative, concurrency, failure, compatibility, or cleanup tests.
4. Benchmark fairness or interpretation errors.
5. Explicit `PASS` or `FAIL` for the phase gate, with file/line evidence.

### Gate Policy

- Any verifier Critical or High issue blocks the phase.
- Medium issues block when they violate a phase acceptance criterion; otherwise they require a tracked owner and target phase.
- The implementation owner resolves feedback, reruns evidence, and requests a fresh independent check.
- The audit report and this plan are updated in the same PR with exact commit/test evidence.
- The next phase starts only after the current phase is merged, deployed where required, observed, and marked `Verified`.

Suggested verifier prompt:

```text
Review Phase <N> against the Bolt remediation plan and source audit findings <IDs>.
Inspect the complete diff and supplied test/benchmark evidence. Do not edit code.
Report missed requirements, partial fixes, regressions, unsafe ownership, missing
negative/concurrency/failure tests, and benchmark interpretation errors. End with
PASS or FAIL and cite exact files/lines for every actionable finding.
```

## PR and Rollout Strategy

1. Use one PR per bounded deliverable; extra-large phases may use ordered sub-PRs but cannot be marked complete until the phase gate passes as a whole.
2. Keep wire/database changes backward compatible until the planned migration gate removes the old path.
3. Use feature flags for Media, protocol v2 negotiation, backplane routing, and new auth shadow validation.
4. Deploy security containment first, then canary one service/client class at a time for auth and protocol migrations.
5. Produce and record a security-qualified rollback digest after Phase 0. It must preserve TLS, Enforce, single-replica operation, feature quarantines, and bounded limits; pre-containment images are prohibited rollback targets.
6. Deploy exact captured `repository@sha256:digest` image references and drain WebSockets before switching.
7. Run authenticated WSS registration, RPC, and pub/sub synthetics after deployment; automatically restore only the security-qualified rollback digest when synthetics or telemetry gates fail. If none exists, fail closed and disable Bolt traffic.
8. Use expand/contract database migrations and versioned Redis keys. Delay destructive cleanup for at least one rollback window.
9. Record rollback instructions before merge. Rollback must preserve security invariants and must not restore Audit, plaintext, shared signing authority, or unbounded limits.
10. Update operational dashboards and alerts before enabling each new path.

## Coverage Check

Primary finding ownership is complete:

- Critical: C1 -> Phases 0-1.
- High: H1 -> Phase 3; H2 -> Phases 0/4; H3-H4 -> Phase 1; H5 -> Phase 4; H6-H9 -> Phase 3; H10-H14 -> Phase 7; H15-H16 -> Phase 4; H17 -> Phase 5; H18-H19/H21 -> Phase 6; H20 -> Phase 6 for non-media routing and Phase 7 for media routing; H22 -> Phases 2/9; H23 -> Phase 3 containment and Phase 5 wire semantics.
- Medium: M1-M3 -> Phase 3; M4-M5 -> Phase 5; M6-M9 -> Phase 7; M10-M11 -> Phase 8; M12 -> Phase 1; M13-M16 -> Phase 6; M17 -> Phase 0.
- Low: L1 -> Phase 9; L2/L4 -> Phases 2/9; L3 -> Phases 7/8; L5 -> Phase 5.

## Completion Definition

The program is complete when all phase gates pass, the source audit contains evidence-backed final dispositions for all 46 findings, no Critical/High finding remains open, the production capability matrix matches tested reality, and performance claims pass the Phase 9 certification matrix on a reproducible dedicated runner.

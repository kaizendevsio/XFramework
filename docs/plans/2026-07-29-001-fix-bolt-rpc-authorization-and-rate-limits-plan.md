---
title: "fix: Bolt RPC Authorization and Rate Limits"
type: fix
status: completed
date: 2026-07-29
---

# Bolt RPC Authorization and Rate Limits Plan

## Status

Completed on 2026-07-29. Functional, security, build, performance, documentation, and independent-review gates pass. The Phase 0 CI workflow ran the complete Bolt suite, including the Docker-backed PostgreSQL topic-authorizer fixture. The pre-existing Storage and Wallets fixtures cannot exercise the centralized service-identity path because they provide neither an IdentityServer token issuer nor equivalent test doubles; this is recorded as separate test-infrastructure debt rather than reported as a product failure or a passing gate. Evidence is recorded in [`bolt-rpc-authorization-rate-limit-results-2026-07-29.md`](../solutions/workflow-issues/bolt-rpc-authorization-rate-limit-results-2026-07-29.md).

## Goal

Close the confirmed authorization and abuse-control gaps in Bolt without moving business authorization into Bolt Hub or expanding Bolt into network security.

The finished design must:

- Bind every generated Bolt RPC invocation to both the authenticated transport sender and an IdentityServer-issued service token for the destination service.
- Keep handler-specific scopes and caller restrictions at the destination service, equivalent to REST endpoint authorization policies.
- Add per-principal RPC request and inbound-byte limits across pooled Bolt connections.
- Remove the unused implicit Push broadcast convention where an empty recipient becomes `recipientHash == 0`.
- Require topic authorization in XFramework Bolt Hub while retaining `CommunicationsBoltTopicAuthorizer` as the current domain policy.
- Preserve Bolt's existing wire version, routing model, timeout guarantees, bounded memory behavior, and performance profile.
- Update canonical knowledge files in the same change so documentation matches the implemented behavior.

## Scope Boundaries

### In scope

- `Bolt.Client`, `Bolt.Server`, XFramework Bolt integration, source-generated Bolt handlers, Hub configuration, Communications topic authorization wiring, metrics, tests, benchmarks, and Bolt knowledge files.
- Generated `[BoltHandler]` RPC handlers.
- Regular unary RPC, large logical RPC admission, and Push admission.
- Optional per-handler required service scopes and allowed service callers.

### Out of scope

- TLS, certificates, Tailscale policy, ingress configuration, or network ACLs.
- Changing `MapBolt()` to require authentication by default. Authentication remains optional for reusable Bolt hosts; XFramework Bolt Hub continues to opt in explicitly.
- Query-string token handling or upstream log redaction.
- A Hub-maintained caller-to-command authorization matrix.
- Media, call signaling, pub/sub delivery-rate limits, TCP, QUIC RPC, or a new Bolt wire version.
- Service-discovery manifest sizing and transient-subscription revalidation, which are separate findings.
- Reintroducing a generic broadcast capability under a different name.

## Target Security Model

Bolt uses two related identities:

1. **Transport identity:** the short-lived `bolt+jwt` validated by Bolt Hub when the WebSocket connects. The Hub binds this identity to the registered service name and client ID.
2. **Invocation identity:** the destination-audience service token in `RequestMetadata.ServiceAccessToken`. The destination service validates this before a generated Bolt handler executes.

For a generated RPC to run, all of the following must be true:

- The transport connection was authenticated and registered by the Hub.
- The invocation token is signed, unexpired, and issued for the destination service.
- The invocation token caller maps to the request frame's verified sender route hash.
- Any handler-specific required scopes are present.
- Any handler-specific allowed-caller rule accepts the caller.

The Hub remains payload-agnostic. It performs frame admission, sender provenance checks, routing, quotas, and rate limiting. The destination performs token and business authorization.

## Phase 0: Baseline and Contract Inventory

### Work

- Preserve the current focused security-test results and the current direct/Hub performance benchmark results before modifying behavior.
- Inventory generated `[BoltHandler]` request types and confirm they expose `RequestMetadata` through the existing request contract.
- Identify intentional infrastructure handlers registered manually with `BoltClient.RegisterHandler` or `BoltServer.RegisterHandler`; these remain outside generated-handler authorization unless they explicitly opt into the new context API.
- Identify destination services that already validate `ServiceAccessToken` so the implementation does not perform duplicate signature validation in one invocation.
- Confirm that production code has no empty-recipient or broadcast `PushAsync` callers.

### Decisions locked by this phase

- REST `RequireAuthorization` metadata will not be blindly translated into Bolt service scopes. REST user authorization and Bolt service authorization are different policies.
- Generated Bolt handlers require baseline destination-token validation by default.
- Optional Bolt scopes and allowed callers are declared explicitly on `BoltHandlerAttribute`.
- Manual low-level handlers preserve compatibility and can use the new inbound context overload when needed.

### Gate

- Publish the inventory in the implementation PR description.
- Stop and revise this plan if a production broadcast caller or a generated Bolt request without metadata is found.

## Phase 1: Push Safety and Per-Principal RPC Limits

### Push behavior

- Reject blank `recipientId` in all `BoltClient.PushAsync` overloads with `ArgumentException`.
- Remove the Hub branch that interprets `recipientHash == 0` as broadcast.
- Treat recipient hash zero like any other route lookup. If no service owns it, record a route miss and do not deliver the frame.
- Preserve the internal large-RPC response Push path, which is validated against its pending invocation before normal Push routing.
- Do not add a replacement broadcast API.

### Rate-limit behavior

- Add configurable per-principal token-bucket limits for:
  - Logical RPC requests per second.
  - Inbound logical RPC and Push payload bytes per second.
- Partition limits by the existing authenticated `QuotaKey`, not by connection, so opening more pooled connections does not multiply allowance.
- Charge one request permit for each normal RPC request and once when a large logical RPC request is admitted.
- Charge byte permits using the declared logical payload size for normal RPC, large RPC, and Push. Do not charge each large-RPC chunk again.
- Apply Push request and byte limits before routing or fan-out work.
- Return `429 Too Many Requests` for rejected unary RPC requests.
- Close a rejected large-RPC stream with `429` before handler dispatch.
- Drop rejected Push frames because Push has no response contract, and record a rejection metric.
- Do not rate-limit responses, response Push frames tied to pending large RPCs, cancellation, acknowledgements, registration, health traffic, or media.
- Do not disconnect a principal for an isolated rejection.

### Configuration

- Add explicit `BoltServerOptions` and `BoltConfiguration` settings for request rate, request burst, byte rate, and byte burst.
- Keep reusable `BoltServer` limits disabled by default.
- Enable them explicitly in XFramework Bolt Hub.
- Select initial Hub values from the Phase 0 single-principal benchmark peak with at least 2x headroom, while retaining hard bounded admission. Record the selected values and evidence in the implementation report rather than guessing them in this plan.

### Metrics and health

- Add counters for request-rate, byte-rate, and Push-rate rejection, tagged only by frame category and reason.
- Expose aggregate limiter configuration and rejection totals in the existing Hub diagnostic snapshot; do not expose principal identities.
- Rate-limit rejection alone does not make readiness unhealthy.

### Tests

- Blank recipient is rejected by raw and typed `PushAsync`.
- A forged zero-recipient Push is not broadcast.
- Requests over the per-principal request limit receive `429`.
- Multiple pooled connections share one principal allowance.
- Different principals have independent allowances.
- Byte admission rejects oversized bursts without retaining payload buffers.
- Large RPC is charged once using declared logical size and closes with `429` when rejected.
- Push over the limit is dropped and measured.
- Response, cancellation, pub/sub, and media behavior is unchanged.
- Limit replenishment and disconnect cleanup do not leak limiter state.

### Review gate

- Independently review permit accounting, pooled-connection partitioning, large-RPC charging, and Push behavior before Phase 2.
- Run focused reliability and performance tests. Do not proceed with a configuration that throttles the existing benchmark matrix.

## Phase 2: Generated RPC Service Authorization

### Inbound request context

- Add an immutable inbound request context carrying the request ID and sender route hash already present in the wire frame.
- Add context-aware `BoltClient.RegisterHandler` overloads while preserving existing overloads for manual handlers.
- Pass the same context through normal and reassembled large-RPC handler dispatch.
- Do not add identity fields to the wire protocol; sender route data already exists.

### Generated-handler guard

- Extend generated Bolt handlers to resolve the existing trusted service-invocation infrastructure before resolving or invoking endpoint services.
- Validate `RequestMetadata.ServiceAccessToken` against the destination service's canonical IdentityServer audience.
- Resolve the token caller and calculate its expected Bolt sender route from the existing deterministic registration rules.
- Reject a valid token whose caller does not match the inbound sender route.
- Run this guard before FluentValidation and before endpoint service resolution.
- Return `401 Unauthorized` for a missing, malformed, expired, wrongly signed, or wrong-audience service token.
- Return `403 Forbidden` for sender/token mismatch, missing required scopes, or a disallowed caller.
- Do not accept tenant IDs, credential IDs, service names, or sender names from unvalidated request metadata as proof of identity.

### Handler policy declaration

- Keep baseline destination-token validation enabled for generated `[BoltHandler]` handlers.
- Add optional `RequiredServiceScopes` and `AllowedServiceCallers` properties to `BoltHandlerAttribute`.
- Emit these policy values directly into generated authorization calls; do not add a central Hub command matrix.
- Keep HTTP endpoint authorization metadata independent unless a future explicit shared-policy contract is approved.

### Validation-result reuse

- Avoid repeated RSA verification when a downstream service context resolver validates the same token again.
- Cache only successful base token validation by token digest and expected audience, bounded by token expiry and a fixed cache-size limit.
- Apply required-scope and allowed-caller checks after retrieving the validated claims so policies cannot contaminate each other's cache entries.
- Never cache failed token validation.
- Never extend validity beyond JWT expiration or existing clock-skew rules.

### Tests

- Valid destination token with matching transport sender reaches the handler.
- Missing, malformed, expired, wrong-signature, and wrong-audience tokens return `401` before handler resolution.
- A token for service A carried by transport sender B returns `403`.
- Missing required scope and disallowed caller return `403`.
- Valid required scope and allowed caller reach the handler.
- Normal and large-RPC paths enforce identical authorization.
- FluentValidation still runs after authorization and preserves its existing response shape.
- Manual handlers using legacy overloads preserve current behavior.
- Successful token validation is reused without accepting a token beyond expiry plus the configured JWT clock skew.
- Generated source snapshots cover default authorization and optional policies.

### Review gate

- Independently review sender-route derivation, audience selection, status semantics, generated code, and cache lifetime.
- Run source-generator, integration-security, IdentityServer, Bolt RPC, large-RPC, and handler-validation tests before Phase 3.

## Phase 3: Required Topic Authorization in XFramework Hub

### Work

- Add `BoltServerOptions.RequireTopicAuthorization`, defaulting to `false` for reusable Bolt hosts.
- Configure XFramework Bolt Hub with `RequireTopicAuthorization = true`.
- Fail Hub startup when topic authorization is required but no `IBoltTopicAuthorizer` is registered.
- Keep `CommunicationsBoltTopicAuthorizer` as the current domain implementation.
- Continue invoking authorization for Subscribe, Publish, Unsubscribe, and Ack.
- Continue denying unknown or malformed topic namespaces through the Communications policy.
- Do not introduce a multi-domain dispatcher or tri-state result until a second topic namespace actually exists.

### Tests

- A reusable Bolt server can intentionally run without topic authorization when the option is false.
- XFramework Hub configuration fails when required authorization has no authorizer.
- Communications topic operations continue to enforce tenant, actor, subscriber, and membership rules.
- Unknown namespaces are denied.
- Authorizer exceptions fail closed.

### Review gate

- Independently verify production Hub wiring and confirm no change to non-pub/sub routing.

## Phase 4: Full Verification and Performance Gate

### Functional verification

- Run the complete `Bolt.Tests` suite.
- Run `XFramework.SourceGenerators.Tests`.
- Run focused IdentityServer service-token tests.
- Run affected Wallets, Storage, Communications, and integration-security tests.
- Run Browser tests if shared Push or protocol-facing APIs change there.
- Run build/test coverage for every service containing generated Bolt handlers.

### Security verification

- Attempt cross-service token substitution, wrong-audience use, expired-token use, sender spoofing, zero-recipient Push, pooled-connection limit bypass, and unknown-topic access.
- Confirm authorization rejects before business handlers execute.
- Confirm logs and metrics contain no access tokens or client secrets.

### Performance verification

- Re-run the existing direct and Hub RPC benchmark matrices on the same machine and configuration used for the Phase 0 baseline.
- Measure mean, p95, throughput, allocations, and CPU for authorized generated-handler calls.
- Verify limiter checks do not materially affect traffic below configured limits.
- Acceptance target: no more than 5% regression in mean or p95 for valid requests below the limit and no material increase in allocated bytes per operation.
- If the gate fails, capture CPU and allocation traces and optimize validation reuse or limiter bookkeeping. Do not bypass authorization to recover benchmark numbers.

## Phase 5: Knowledge and Configuration Updates

Update knowledge only after final behavior and names are stable. The implementation is incomplete until these files agree with the code.

### Canonical knowledge files

- Update `src/Libraries/Bolt/BOLT.md` with:
  - Transport identity versus invocation identity.
  - Generated-handler authorization order and status semantics.
  - Explicit-recipient Push behavior and removal of implicit broadcast.
  - Per-principal RPC request/byte limiting.
  - Optional reusable-host authentication and topic-authorization settings.
- Update `docs/solutions/architecture-patterns/bolt-unified-transport-layer.md` with the final trust boundaries and request lifecycle.
- Update `docs/solutions/architecture-patterns/bolt-hub-operational-constraints-and-exceptions.md` with required Hub topic authorization and the continued Communications-specific read exception.
- Update `docs/solutions/conventions/xframework-best-practices.md` with generated Bolt handler service-token, scope, and allowed-caller conventions.

### Historical and audit records

- Update `docs/solutions/workflow-issues/bolt-hub-protocol-audit-issues.md` with implementation status and links to tests/results without rewriting unrelated historical findings.
- Update the current Bolt remediation/investigation record only where it describes behavior changed by this work.
- Mark this plan `completed` only after implementation, verification, documentation, and review gates pass.

### Configuration examples

- Document every new rate-limit and topic-authorization option beside its configuration model.
- Update relevant Hub `appsettings` examples with explicit values and comments in the durable knowledge files.
- Preserve `MapBolt()` authentication as opt-in and document that XFramework Bolt Hub explicitly requires it.

### Freshness check

- Search the repository for stale claims that blank Push recipients broadcast, that generated Bolt handlers skip service authorization, or that missing topic authorizers are acceptable in XFramework Hub.
- Ensure new terminology is consistent across code comments, tests, `BOLT.md`, architecture notes, and audit records.

## Expected Change Surface

Likely implementation files include:

- `src/Libraries/Bolt/Bolt.Client/BoltClient.cs`
- `src/Libraries/Bolt/Bolt.Server/BoltServer.cs`
- `src/Libraries/Bolt/Bolt.Server/BoltServerExtensions.cs`
- `src/Libraries/Bolt/Bolt.Server/BoltServerMetrics.cs`
- `src/Infrastructure/XFramework.Integration/Attributes/BoltHandlerAttribute.cs`
- `src/Infrastructure/XFramework.Integration/Security/*`
- `src/Infrastructure/XFramework.Integration/Extensions/ServiceCollectionExtensions.cs`
- `src/SourceGenerators/XFramework.SourceGenerators/BoltHandlerGenerator.cs`
- `src/Shared/XFramework.Domain.Shared/Configurations/BoltConfiguration.cs`
- `src/Modules/XFramework.Bolt/Bolt.Hub/Installers/BoltInstaller.cs`
- Bolt, source-generator, IdentityServer, and affected module tests.
- The knowledge files listed in Phase 5.

This list is directional, not permission for unrelated refactoring.

## Completion Criteria

The plan is complete only when:

- Generated Bolt handlers reject missing or invalid destination service tokens.
- Token caller identity is bound to the Hub-verified sender route.
- Optional scopes and allowed callers are enforced at destination handlers.
- Empty-recipient Push cannot broadcast and zero-recipient frames do not fan out.
- RPC request and byte limits are shared by principal across pooled connections and produce correct `429` or drop behavior.
- XFramework Hub cannot start with required topic authorization missing.
- All focused and full affected test suites pass.
- Performance remains within the stated gate.
- Canonical knowledge, configuration examples, and audit records reflect the implemented behavior.
- An independent final review finds no unresolved High or Critical issue within this plan's scope.

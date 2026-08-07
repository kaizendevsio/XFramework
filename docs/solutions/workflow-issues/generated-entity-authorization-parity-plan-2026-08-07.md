# Generated Entity Authorization Parity Plan

## Status

Implemented and verified on 2026-08-07. Independent review findings were addressed before PR publication.

## Problem

`GenerateEndpointsAttribute.Roles` is currently enforced by generated REST endpoints, but equivalent entity-specific authorization is not consistently enforced by generated Bolt services or remote `IDataContext` queries. `DataContextBoltHandler` relies on coarse transport scopes, while `QueryExecutionService` only maps selected IdentityServer entities to capability checks. This allows an authenticated service caller to reach generated entity reads without the same delegated actor authorization required by REST.

## Target Architecture

Generate one server-owned authorization descriptor per entity and operation. REST endpoints, generated entity services, Bolt handlers, and remote `IDataContext` execution must consume the same descriptor and centralized evaluator.

Each descriptor records:

- Entity and operation: read, create, update, or delete.
- Actor requirement.
- Allowed actor roles, using any-of semantics.
- Required actor capability.
- Optional structured actor attribute requirements.
- Tenant access mode.
- Required service scopes and allowed service callers.
- Remote query and mutation exposure.
- A policy version used for diagnostics and cache isolation.

Policy metadata supplied by a caller is never authoritative. The receiving service resolves policy from its generated registry.

## Phase 1: Canonical Metadata Contract

Ownership: `XFramework.Domain.Shared` and `XFramework.Core`.

- Make `XFramework.Domain.Shared.Attributes.GenerateEndpointsAttribute` canonical.
- Convert the Core copy into a temporary obsolete forwarding shim, migrate first-party usages, and remove the shim in a later breaking release.
- Add common metadata to `GenerateEndpointsAttribute`:
  - Authorization feature, such as `wallets.reporting`.
  - Per-operation capability keys defaulting to `view`, `create`, `update`, and `delete`.
  - Existing `Roles`, retaining any-of behavior.
  - Actor requirement, defaulting to required when `RequireAuthorization` is true.
- Add structured companion metadata for uncommon actor attribute requirements and explicit service-only access.
- Public REST access must not automatically enable anonymous remote `IDataContext` access.

## Phase 2: IdentityServer And Trusted Actor Model

Ownership: `IdentityServer.Domain.Shared`, `IdentityServer.Api`, `IdentityServer.Integration`, and `XFramework.Integration`.

- Keep the canonical capability grammar in IdentityServer: `{module}[.{subfeature}]:{view|create|update|delete|manage}`.
- Continue using `TenantModuleFeatureKeys` and `IdentityAuthorizationConstants` as the taxonomy authority.
- Extend the validated actor snapshot with an immutable, allowlisted attribute collection.
- Populate actor attributes only from IdentityServer-validated session state, never from request metadata or unvalidated claims.
- Extend `InvocationAuthorizationPolicy` with required roles, capabilities, and structured actor attributes.
- Preserve `IActorIdentityProvider` as the provider boundary so IdentityServer remains the default but replaceable implementation.
- Validate actor and service credentials once, then evaluate one or more policies against the resulting immutable identities.

## Phase 3: Source Generator Propagation

Ownership: `XFramework.SourceGenerators`.

- `EntityEndpointGenerator` emits and enforces the operation descriptor. ASP.NET authorization metadata remains useful for routing and documentation but is not the authoritative role check.
- `EntityServiceGenerator` enforces the same descriptor against the already resolved trusted invocation context, preventing direct service invocation from bypassing policy.
- `ServiceWrapperGenerator` propagates the actor token, trusted tenant target, entity, and operation. It never sends caller-controlled policy metadata or falls back to service-only access.
- `DataContextRegistrationGenerator` emits an immutable entity policy registry with separate read/create/update/delete descriptors and diagnostics for invalid or contradictory policies.

## Phase 4: Bolt And Remote DataContext Enforcement

Ownership: `XFramework.Core`.

- `DataContextBoltHandler` resolves the entity and operation before authorization and evaluates the server-generated policy.
- Multi-entity `SaveChangesRequest` operations authorize every entity and operation before a transaction or database command begins.
- `QueryExecutionService` replaces the hard-coded IdentityServer entity capability map with the generated policy registry.
- Policy is enforced before query compilation, cache lookup, or EF execution.
- `TrustedInvocationContext.EffectiveTenantId` is authoritative; request metadata cannot override it.
- Tenant feature checks use the descriptor's explicit feature key instead of route inference.

## Identity Rules

- A service token identifies the calling service and never substitutes for a user.
- User-driven calls require a delegated actor when the generated policy requires one.
- The actor tenant is authoritative unless an explicit cross-tenant capability and delegated tenant policy allow another target.
- Background calls require explicit service-only metadata, an allowlisted caller, exact service scopes, and an authorized target tenant.
- Role, capability, attribute, service, and tenant requirements are cumulative when configured together.

## Denial Contract

- Missing or invalid required actor: `401`.
- Insufficient actor role, capability, or attribute: `403`.
- Actor and requested tenant mismatch: `403`.
- Service-token-only access to an actor-required entity: `401`.
- Missing service scope or disallowed service caller: `403`.
- Unauthorized cross-tenant delegation: `403`.
- Public REST entity queried remotely without explicit remote authorization: `403`.
- Authorization failures occur before database access.

## Compatibility Strategy

- Initially translate existing `RequireAuthorization = true` and `Roles` into an actor-required descriptor on every generated path. This closes the service-only gap while preserving current REST behavior.
- Emit a warning when a secured generated entity lacks an authorization feature.
- Migrate all first-party modules, then promote the warning to a build error.
- Keep service-only access default-denied throughout.
- `RequireAuthorization = false` remains a REST decision and does not enable anonymous remote access.

## Caching And Token Acquisition

- Generated policy registries are immutable singletons and require no distributed cache.
- Do not add authorization decision caching initially.
- If decision caching is later required, key by actor credential, session and generation, effective tenant, service identity and generation, entity, operation, and policy version. TTL cannot exceed token expiry.
- Query caches include actor authority and policy version so differently authorized actors cannot share sensitive cached results.
- Generated wrappers use stable generic service scopes to avoid service-token cache fragmentation.
- Existing coalesced service-token acquisition must remain, with concurrency coverage proving one acquisition per audience and scope set.

## Test Plan

- Source-generator tests cover descriptors, all affected generators, compatibility behavior, and diagnostics.
- Core tests cover trusted policy evaluation, Bolt handler enforcement, multi-entity mutation, and `QueryExecutionService` defense-in-depth.
- IdentityServer tests cover role, capability, attribute, generation, and tenant propagation.
- Cross-module integration tests assert REST, generated Bolt wrappers, and remote `IDataContext` produce the same allow or deny outcome.
- Required denial cases include missing actor, wrong tenant, insufficient role, capability, or attribute, service-token-only access, disallowed service caller, and unauthorized mixed-entity mutation.
- Denied operations must execute zero EF commands.
- A completeness guard requires every remotely exposed generated entity to have a generated authorization descriptor.

## Wallets Migration After Shared Infrastructure

- Use `wallets:view` for permitted account-facing reads.
- Use `wallets.reporting:view` for operations, ledger, snapshots, transactions, deposits, withdrawals, cases, and audit/reporting reads.
- Use `wallets.policy:manage` for approvals, policies, and fee administration.
- Use `wallets.reconciliation:manage` for reconciliation entities.
- Use `wallets.webhooks:manage` for outbox and webhook audit entities.
- Use `wallets:update` or `wallets:manage` only for appropriate generated mutations.
- Retain Admin or SuperAdmin roles only where the entity is genuinely administrator-only; do not block a configured read-only role from capability-authorized reads.
- Remove generic remote exposure where entity-wide tenant access is too broad and use ownership-aware workflow endpoints instead.

Wallets must not add one-off Bolt or `QueryExecutionService` authorization recipes while this shared mechanism is being implemented. Custom endpoint capability hardening may proceed independently, but the generated entity finding remains open until shared enforcement and Wallets policy declarations are complete.

## Implementation Record

- The shared attribute now owns generated authorization metadata; the Core attribute is an obsolete forwarding shim.
- Source generators emit one immutable operation policy registry consumed by generated REST endpoints, entity services, Bolt wrappers, and remote `IDataContext` handlers.
- Trusted actor roles, capabilities, attributes, tenant authority, service identity, caller allowlists, and scopes are evaluated centrally before business or EF execution.
- Remote mutation uses a complete zero-I/O policy/trust preflight before deduplicated feature checks and mutation context resolution.
- Authorization-aware query cache partitions include actor/service authority, policy version, and either the trusted tenant or an explicit tenantless segment.
- IdentityServer and Wallets generated entities declare canonical feature/capability metadata, with completeness guards preventing unprotected remote exposure.
- Wallets maps and registers generated entity REST services. `Wallet` remains service-only generation because its custom REST endpoint owns the public `GetWallet` route.

## Verification Record

- Source-generator tests cover metadata propagation, canonical taxonomy diagnostics, the Core compatibility shim inputs, REST-only exclusion, and typed wrapper denials.
- Core tests cover role, capability, attribute, actor/tenant mismatch, service-token-only calls, disallowed callers/scopes, immutable policy snapshots, tenantless feature/cache behavior, typed `401`/`403`, and mixed-mutation zero-I/O preflight.
- IdentityServer integration tests cover missing-actor REST, generated wrapper, and remote `IDataContext` denials plus policy completeness.
- Wallets integration tests use a real secured entity to prove successful and insufficient-capability reads have matching outcomes through REST, generated Bolt wrapper, and remote `IDataContext`.
- Wrong-target-tenant checks apply to delegated wrapper/remote invocation because generated actor-tenant REST reads have no caller-supplied target tenant. Role/attribute and service-only matrices remain centralized tests until a first-party generated entity declares those optional policy dimensions; production entities were not given artificial requirements solely for testing.

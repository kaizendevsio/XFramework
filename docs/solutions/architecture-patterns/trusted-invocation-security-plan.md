# Trusted Invocation Security Plan

## Summary

Replace metadata-based identity with a centralized, immutable `TrustedInvocationContext` created before REST or Bolt handlers execute.

IdentityServer remains the default authority for actor identity, sessions, roles, capabilities, and tenant membership. Vault owns protected key and service-credential material. Shared XFramework infrastructure coordinates both through replaceable provider interfaces.

This is an intentional breaking migration. Legacy identity fields in `RequestMetadata` will be removed rather than supported indefinitely.

## 1. Define Security Boundaries

Create three distinct models.

### Transport Credentials

Credentials travel outside business requests:

```csharp
public sealed record InvocationCredentials(
    string? ActorAccessToken,
    string? ServiceAccessToken);
```

- REST uses authorization headers.
- Bolt uses protocol or envelope fields.
- Tokens are never placed in `RequestMetadata`.
- Bolt connection tokens remain separate from per-invocation service and actor tokens.

### Trusted Invocation Context

Create an immutable server-owned context:

```csharp
public sealed record TrustedInvocationContext(
    TrustedActorIdentity? Actor,
    TrustedServiceIdentity? Service,
    Guid? EffectiveTenantId,
    Guid? RequestedTargetTenantId,
    string CorrelationId);
```

`TrustedActorIdentity` contains validated:

- Credential ID
- Identity or user ID
- Tenant ID
- Session ID
- Roles and capabilities
- Session or JWT generation
- Token expiry

`TrustedServiceIdentity` contains validated:

- Client or service ID
- Audience
- Scopes
- Credential generation
- Allowed caller information

Modules can read this context but cannot construct or modify it.

### Request Metadata

Reduce `RequestMetadata` to:

- Correlation or request ID
- Device information
- User agent
- IP address
- Operation name
- Requested target tenant, only for explicit delegated operations

Remove:

- `SessionId`
- `TenantId` as effective identity
- `CredentialId`
- `ActorAccessToken`
- `ServiceAccessToken`
- `ActorTenantId`
- `HasTrustedActorContext`
- `TrustedActorRoles`

## 2. Add Replaceable Validation Providers

Add shared abstractions in `XFramework.Integration.Security`:

```csharp
public interface IActorIdentityProvider
{
    Task<ActorValidationResult> ValidateAsync(
        string token,
        ActorValidationOptions options,
        CancellationToken cancellationToken);
}

public interface IServiceIdentityProvider
{
    Task<ServiceValidationResult> ValidateAsync(
        string token,
        ServiceValidationOptions options,
        CancellationToken cancellationToken);
}
```

Add an `ITrustedInvocationResolver` that:

1. Receives transport credentials.
2. Calls the configured providers.
3. Applies endpoint authorization policy.
4. Resolves the effective tenant.
5. Produces `TrustedInvocationContext`.

Source generators and modules depend only on these abstractions.

## 3. Implement Default IdentityServer Providers

IdentityServer becomes the default actor-validation provider.

Actor validation must cover:

- Signature, issuer, audience, and expiry
- Credential and tenant claims
- Session existence and active status
- Session or JWT generation
- Credential status
- Identity status
- Current roles and capabilities
- Revocation or logout state

Reuse the existing IdentityServer session validation logic rather than creating a second validation recipe.

Service-token validation must cover:

- Signature and signing-key rotation
- Issuer and audience
- Client or service ID
- Service credential generation
- Required scopes
- Token expiry
- Allowed caller policy

Validation results return structured identities, not mutable metadata.

## 4. Define Vault Ownership

Vault owns:

- Signing and verification key material
- Service credentials and key rotation
- Secure retrieval of current and previous verification keys
- Protection of secrets at rest

IdentityServer owns what authenticated claims mean and whether an actor or session remains valid.

Business modules do not call Vault or IdentityServer directly. They consume the shared provider interfaces. Vault must not decide tenant membership, roles, or business authorization.

The initial implementation should adapt the current service-identity key flow. A broader Vault redesign is out of scope unless required to remove an existing insecure key path.

## 5. Centralize Tenant Authorization

Introduce an explicit tenant access policy for every protected endpoint or handler:

- `ActorTenant`: target is always the actor's tenant.
- `DelegatedTenant`: another tenant is allowed only with an actor capability and endpoint permission.
- `ServiceTargetTenant`: actorless background work requires an explicit service scope and allowed caller.
- `Tenantless`: only for genuinely global operations.

Rules:

1. Actor request without a target: use actor tenant.
2. Actor request with matching target: allow.
3. Actor request with a different target: require signed cross-tenant authority and endpoint opt-in.
4. Service-only target: require tenant-targeting scope and endpoint caller policy.
5. Otherwise: reject.
6. Never silently trust or repair a tenant mismatch.

Presence of a tenant ID is not proof of authorization.

## 6. Bake Validation Into Generated Bolt Handlers

Update Bolt handler generation so every protected invocation follows this order:

1. Read transport credentials from the Bolt envelope.
2. Validate Bolt sender and service-token binding.
3. Resolve actor and service identities.
4. Apply handler scopes, allowed callers, actor requirements, and tenant policy.
5. Establish a scoped `TrustedInvocationContext`.
6. Sanitize `RequestMetadata`.
7. Run request validation.
8. Invoke the business service.
9. Dispose the invocation scope.

Extend existing Bolt handler metadata instead of creating parallel authorization mechanisms.

Default behavior should be restrictive:

- Business handlers require an actor and use the actor tenant.
- Actorless service operations require explicit opt-in.
- Tenantless handlers require explicit declaration.

Bolt Hub transports credentials but does not become the authority for actor claims. The destination service validates the invocation through the configured providers.

## 7. Apply the Same Pipeline to REST

Update generated REST endpoints and relevant handwritten endpoints to:

1. Read the authenticated principal and transport credentials.
2. Resolve `TrustedInvocationContext`.
3. Enforce tenant and endpoint policy.
4. Run validators only after trusted context creation.
5. Invoke services with the scoped context.

IdentityServer's current `ApplyHttpContextActor` metadata mutation should be replaced by context creation.

REST and Bolt must use the same policy resolver and produce equivalent authorization decisions.

## 8. Update Wrappers and Portal Propagation

Update generated service wrappers and `BoltDriver`:

- Obtain actor tokens from an `IActorTokenAccessor`.
- Obtain service tokens from the existing centralized service-token provider.
- Put both in transport credentials, not request metadata.
- Do not generate identity fields from entity properties.

Update Portal authentication:

- Retain the IdentityServer actor token in protected authentication state.
- Resolve it server-side for wrapper calls.
- Do not trust browser-submitted credential or tenant IDs.
- Treat selected tenant as a requested delegated target.
- Require a signed cross-tenant capability for tenant administration.

Background services send only service identity unless they are explicitly propagating a real actor.

## 9. Secure Remote `IDataContext`

Apply the same trusted invocation pipeline to:

- Remote queries
- Save operations
- Query capability checks
- Cache-key tenant resolution

Remote `IDataContext` must derive tenant and credential identity from `TrustedInvocationContext`.

Cross-tenant query capabilities must come from validated actor capabilities or explicit service scopes. They must not be inferred from `QueryDescriptor.Metadata`.

## 10. Migrate Modules

Audit all modules for:

- `Metadata.TenantId`
- `Metadata.CredentialId`
- `ActorTenantId`
- `ActorAccessToken`
- `HasTrustedActorContext`
- Local JWT decoding
- Local token validators
- Module-specific tenant resolver recipes

Replace these with `ITrustedInvocationContextAccessor` or an explicit context service parameter.

Priority:

1. Inventario because of the known cross-tenant exposure.
2. IdentityServer authorization and administration.
3. Wallets and POS because of financial sensitivity.
4. Communications and Storage.
5. Remaining modules and Portal services.

During migration:

- Remove duplicated request-context resolvers.
- Narrow `IgnoreQueryFilters()` usage.
- Validate every entity ID against `EffectiveTenantId`.
- Ensure tenant-scoped cache keys use the trusted tenant.
- Keep trusted context out of serialized responses and logs.

## 11. Remove Legacy Compatibility

Once all callers compile against the new contract:

- Delete `TrustedServiceInvocationResolver` metadata-based behavior.
- Delete trusted flags from `RequestMetadata`.
- Delete module-specific token decoding.
- Delete fallback paths that accept metadata identity.
- Prevent request contracts from reintroducing actor or service tokens.
- Add architecture tests that prohibit forbidden fields and patterns.

No permanent compatibility adapter should remain.

## 12. Test Coverage

### Shared Security Tests

Cover:

- Valid actor and service tokens
- Expired, malformed, and revoked tokens
- Wrong issuer or audience
- Missing scopes
- Service credential generation mismatch
- Actor session generation mismatch
- Bolt sender and service caller mismatch
- Provider replacement through DI

### Tenant Isolation Tests

Cover both REST and Bolt:

- Caller supplies another tenant
- Actor and request tenant mismatch
- Cross-tenant entity ID
- Authorized delegated administration
- Delegated target without capability
- Authorized background tenant targeting
- Service target without scope
- Actorless protected request
- Explicit tenantless operation

### Generator Tests

Assert generated handlers:

- Validate before request validators and business services
- Establish and clear scoped context
- Never use identity fields from metadata
- Enforce restrictive defaults
- Pass policy metadata correctly

### Integration Tests

Add wrapper-level tests for:

- Portal user calls
- Admin delegated tenant calls
- Background service calls
- Remote `IDataContext`
- Logout and revoked-session behavior
- Role and capability changes taking effect
- Cross-tenant denial despite `IgnoreQueryFilters()`

## 13. Implementation Sequence

1. Add new models, provider interfaces, policy model, and context accessor.
2. Implement IdentityServer actor and service providers and the Vault key adapter.
3. Add the centralized tenant resolver.
4. Extend Bolt protocol credentials and the generated handler pipeline.
5. Apply the same resolver to REST handlers.
6. Update wrappers, Portal token propagation, and remote `IDataContext`.
7. Migrate every module.
8. Remove legacy metadata identity fields and local validators.
9. Run repository-wide architecture, generator, integration, and runtime tests.
10. Perform a final independent audit against this plan.

Because this is a coordinated breaking change, implementation should remain on one branch until all callers compile and the legacy path is removed. Partial deployment would otherwise mix old metadata-based callers with new strict handlers.

## Acceptance Criteria

- No module validates or decodes actor or service tokens itself.
- No business service derives identity from `RequestMetadata`.
- IdentityServer is the default actor and service validation provider.
- Providers are replaceable through dependency injection.
- Vault owns secure key and credential material, not tenant authorization.
- REST and Bolt enforce the same invocation policy.
- Effective tenant always comes from trusted identity or explicitly authorized delegation.
- Tokens never appear in business request metadata.
- Inventario tenant-spoofing regression tests pass.
- Architecture tests prevent the old pattern from returning.

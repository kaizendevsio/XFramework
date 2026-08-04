using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.ServiceIdentity;

namespace XFramework.Integration.Security;

public sealed class TrustedInvocationResolver(
    IActorIdentityProvider actorIdentityProvider,
    IServiceIdentityProvider serviceIdentityProvider)
    : ITrustedInvocationResolver
{
    public async Task<TrustedInvocationResult> ResolveAsync(
        InvocationCredentials credentials,
        RequestMetadata metadata,
        InvocationAuthorizationPolicy policy,
        string expectedAudience,
        CancellationToken ct = default)
    {
        TrustedServiceIdentity? service = null;
        if (!string.IsNullOrWhiteSpace(credentials.ServiceAccessToken))
        {
            var validation = await serviceIdentityProvider.ValidateAsync(
                credentials.ServiceAccessToken,
                expectedAudience,
                ct);
            if (!validation.IsValid)
                return TrustedInvocationResult.Failure(validation.Error!, validation.StatusCode);

            service = validation.Identity;
        }
        else if (policy.RequireServiceIdentity)
        {
            return TrustedInvocationResult.Failure("Service identity is required.");
        }

        if (service is not null)
        {
            var missingScopes = policy.RequiredServiceScopes
                .Where(scope => !service.Scopes.Contains(scope))
                .ToArray();
            if (missingScopes.Length > 0)
            {
                return TrustedInvocationResult.Failure(
                    $"Service token is missing required scope(s): {string.Join(", ", missingScopes)}.",
                    403);
            }

            if (policy.AllowedServiceCallers.Count > 0 &&
                !policy.AllowedServiceCallers.Contains(service.ClientId, StringComparer.OrdinalIgnoreCase))
            {
                return TrustedInvocationResult.Failure("Service caller is not allowed.", 403);
            }
        }

        TrustedActorIdentity? actor = null;
        if (!string.IsNullOrWhiteSpace(credentials.ActorAccessToken))
        {
            var validation = await actorIdentityProvider.ValidateAsync(credentials.ActorAccessToken, ct);
            if (!validation.IsValid)
                return TrustedInvocationResult.Failure(validation.Error!, validation.StatusCode);

            actor = validation.Identity;
        }
        else if (policy.ActorRequirement == ActorRequirement.Required)
        {
            return TrustedInvocationResult.Failure("Actor identity is required.");
        }

        if (policy.ActorRequirement == ActorRequirement.None && actor is not null)
            return TrustedInvocationResult.Failure("This operation does not accept actor delegation.", 403);

        if (actor is not null &&
            policy.RequiredActorCapabilities.Any(capability => !actor.Capabilities.Contains(capability)))
        {
            return TrustedInvocationResult.Failure(
                "Actor is not authorized for this operation.",
                403);
        }

        if (policy.AllowAnonymous)
        {
            var anonymousPolicy = ValidateAnonymousPolicy(policy);
            if (!anonymousPolicy.IsSuccess)
                return anonymousPolicy;
        }
        else if (actor is null && service is null)
        {
            return TrustedInvocationResult.Failure(
                "A validated actor or service identity is required.");
        }

        var tenantResult = ResolveTenant(actor, service, metadata.RequestedTenantId, policy);
        if (!tenantResult.IsSuccess)
            return tenantResult;

        var context = new TrustedInvocationContext(
            actor,
            service,
            tenantResult.Context!.EffectiveTenantId,
            metadata.RequestedTenantId,
            metadata.RequestId is { } requestId && requestId != Guid.Empty ? requestId : Guid.NewGuid());

        return TrustedInvocationResult.Success(context);
    }

    private static TrustedInvocationResult ResolveTenant(
        TrustedActorIdentity? actor,
        TrustedServiceIdentity? service,
        Guid? requestedTenantId,
        InvocationAuthorizationPolicy policy)
    {
        switch (policy.TenantAccessMode)
        {
            case TenantAccessMode.ActorTenant:
                if (actor is null)
                    return TrustedInvocationResult.Failure("An actor tenant is required.");
                if (requestedTenantId is { } actorTarget && actorTarget != actor.TenantId)
                    return TrustedInvocationResult.Failure("Requested tenant does not match the authenticated actor tenant.", 403);
                return TenantSuccess(actor.TenantId);

            case TenantAccessMode.DelegatedTenant:
                if (actor is null)
                    return TrustedInvocationResult.Failure("An actor identity is required for delegated tenant access.");
                if (requestedTenantId == Guid.Empty)
                    return TrustedInvocationResult.Failure("A requested target tenant cannot be empty.", 400);
                if (requestedTenantId is null || requestedTenantId == actor.TenantId)
                    return TenantSuccess(actor.TenantId);
                var crossTenantCapabilities = policy.RequiredCrossTenantActorCapabilities.Count > 0
                    ? policy.RequiredCrossTenantActorCapabilities
                    : policy.RequiredActorCapabilities;
                if (crossTenantCapabilities.Count == 0)
                    return TrustedInvocationResult.Failure("Delegated tenant access requires explicit actor capabilities.", 403);
                if (crossTenantCapabilities.Any(capability => !actor.Capabilities.Contains(capability)))
                {
                    return TrustedInvocationResult.Failure("Actor is not authorized for delegated tenant access.", 403);
                }
                return TenantSuccess(requestedTenantId.Value);

            case TenantAccessMode.ServiceTargetTenant:
                if (service is null)
                    return TrustedInvocationResult.Failure("A service identity is required for tenant targeting.");

                if (actor is not null &&
                    requestedTenantId is { } actorServiceTarget &&
                    actorServiceTarget != actor.TenantId)
                {
                    return TrustedInvocationResult.Failure(
                        "Requested tenant does not match the authenticated actor tenant.",
                        403);
                }

                if (!policy.RequiredServiceScopes.Contains(
                        XFrameworkServiceScopes.TenantTarget,
                        StringComparer.OrdinalIgnoreCase))
                {
                    return TrustedInvocationResult.Failure(
                        $"Service tenant targeting requires the '{XFrameworkServiceScopes.TenantTarget}' scope policy.",
                        403);
                }

                if (policy.AllowedServiceCallers.Count == 0)
                {
                    return TrustedInvocationResult.Failure(
                        "Service tenant targeting requires an explicit caller allowlist.",
                        403);
                }

                if (requestedTenantId is not { } serviceTarget || serviceTarget == Guid.Empty)
                    return TrustedInvocationResult.Failure("A requested target tenant is required.", 400);
                return TenantSuccess(serviceTarget);

            case TenantAccessMode.PublicTenantLookup:
                if (!policy.AllowAnonymous)
                    return TrustedInvocationResult.Failure("Public tenant lookup requires an anonymous policy.", 500);
                if (actor is not null)
                {
                    if (requestedTenantId is { } publicActorTarget && publicActorTarget != actor.TenantId)
                    {
                        return TrustedInvocationResult.Failure(
                            "Requested tenant does not match the authenticated actor tenant.",
                            403);
                    }

                    return TenantSuccess(actor.TenantId);
                }
                if (requestedTenantId is not { } publicTarget || publicTarget == Guid.Empty)
                    return TrustedInvocationResult.Failure("A requested target tenant is required.", 400);
                return TenantSuccess(publicTarget);

            case TenantAccessMode.Tenantless:
                if (requestedTenantId is not null)
                    return TrustedInvocationResult.Failure("This operation does not accept a target tenant.", 400);
                return TenantSuccess(null);

            default:
                return TrustedInvocationResult.Failure("Unsupported tenant access policy.", 500);
        }
    }

    private static TrustedInvocationResult ValidateAnonymousPolicy(InvocationAuthorizationPolicy policy)
    {
        if (!policy.AllowAnonymous)
        {
            return TrustedInvocationResult.Failure(
                "A validated actor or service identity is required.");
        }

        if (policy.RequireServiceIdentity ||
            policy.RequiredServiceScopes.Count > 0 ||
            policy.AllowedServiceCallers.Count > 0 ||
            policy.RequiredActorCapabilities.Count > 0 ||
            policy.RequiredCrossTenantActorCapabilities.Count > 0)
        {
            return TrustedInvocationResult.Failure(
                "Anonymous access cannot be combined with service or actor authorization requirements.",
                500);
        }

        var publicAuthentication =
            policy.ActorRequirement == ActorRequirement.Optional &&
            policy.TenantAccessMode == TenantAccessMode.PublicTenantLookup;
        var publicTenantlessDiscovery =
            policy.ActorRequirement is ActorRequirement.None or ActorRequirement.Optional &&
            policy.TenantAccessMode == TenantAccessMode.Tenantless;

        return publicAuthentication || publicTenantlessDiscovery
            ? TenantSuccess(null)
            : TrustedInvocationResult.Failure(
                "Anonymous access is not valid for this authorization policy.",
                500);
    }

    private static TrustedInvocationResult TenantSuccess(Guid? tenantId) =>
        TrustedInvocationResult.Success(new TrustedInvocationContext(
            null,
            null,
            tenantId,
            tenantId,
            Guid.Empty));
}

using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.ServiceIdentity;

namespace XFramework.Integration.Security;

public sealed class TrustedServiceTargetContextInitializer(
    IServiceTokenProvider serviceTokenProvider,
    ITrustedInvocationResolver invocationResolver,
    ITrustedInvocationContextStore contextStore)
    : ITrustedServiceTargetContextInitializer
{
    public async Task<TrustedInvocationResult> EstablishAsync(
        Guid targetTenantId,
        string audience,
        IReadOnlyCollection<string> requiredServiceScopes,
        string allowedServiceCaller,
        Guid? correlationId = null,
        CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(targetTenantId, Guid.Empty);
        ArgumentException.ThrowIfNullOrWhiteSpace(audience);
        ArgumentException.ThrowIfNullOrWhiteSpace(allowedServiceCaller);

        var scopes = requiredServiceScopes
            .Append(XFrameworkServiceScopes.TenantTarget)
            .Where(static scope => !string.IsNullOrWhiteSpace(scope))
            .Select(static scope => scope.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var serviceToken = await serviceTokenProvider.GetTokenAsync(audience, scopes, ct);
        var metadata = new RequestMetadata
        {
            RequestedTenantId = targetTenantId,
            RequestId = correlationId is { } requestId && requestId != Guid.Empty
                ? requestId
                : Guid.NewGuid()
        };
        var authorization = await invocationResolver.ResolveAsync(
            new InvocationCredentials(null, serviceToken),
            metadata,
            new InvocationAuthorizationPolicy
            {
                ActorRequirement = ActorRequirement.None,
                TenantAccessMode = TenantAccessMode.ServiceTargetTenant,
                RequireServiceIdentity = true,
                RequiredServiceScopes = scopes,
                AllowedServiceCallers = [allowedServiceCaller]
            },
            audience,
            ct);

        if (authorization.IsSuccess)
            contextStore.Set(authorization.Context!);

        return authorization;
    }

    public async Task<TrustedInvocationResult> EstablishTenantlessAsync(
        string audience,
        IReadOnlyCollection<string> requiredServiceScopes,
        string allowedServiceCaller,
        Guid? correlationId = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(audience);
        ArgumentException.ThrowIfNullOrWhiteSpace(allowedServiceCaller);

        var scopes = requiredServiceScopes
            .Where(static scope => !string.IsNullOrWhiteSpace(scope))
            .Select(static scope => scope.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var serviceToken = await serviceTokenProvider.GetTokenAsync(audience, scopes, ct);
        var metadata = new RequestMetadata
        {
            RequestId = correlationId is { } requestId && requestId != Guid.Empty
                ? requestId
                : Guid.NewGuid()
        };
        var authorization = await invocationResolver.ResolveAsync(
            new InvocationCredentials(null, serviceToken),
            metadata,
            new InvocationAuthorizationPolicy
            {
                ActorRequirement = ActorRequirement.None,
                TenantAccessMode = TenantAccessMode.Tenantless,
                RequireServiceIdentity = true,
                RequiredServiceScopes = scopes,
                AllowedServiceCallers = [allowedServiceCaller]
            },
            audience,
            ct);

        if (authorization.IsSuccess)
            contextStore.Set(authorization.Context!);

        return authorization;
    }
}

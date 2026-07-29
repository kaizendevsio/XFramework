using XFramework.Domain.Shared.BusinessObjects;

namespace XFramework.Integration.Security;

public sealed class TrustedServiceInvocationResolver(IServiceTokenValidator tokenValidator)
    : ITrustedServiceInvocationResolver
{
    public async Task<TrustedServiceInvocationResult> ResolveAsync(
        RequestMetadata? metadata,
        string expectedAudience,
        IReadOnlyCollection<string>? requiredScopes = null,
        IReadOnlyCollection<string>? allowedCallers = null,
        bool requireTenant = true,
        CancellationToken ct = default)
    {
        if (metadata is null)
            return TrustedServiceInvocationResult.Failure("Request metadata is required.");

        var validation = await tokenValidator.ValidateAsync(
            metadata.ServiceAccessToken,
            expectedAudience,
            requiredScopes: null,
            ct);

        if (!validation.IsValid)
            return TrustedServiceInvocationResult.Failure(
                validation.Error ?? "Service token is invalid.",
                validation.FailureStatusCode);

        if (requiredScopes is { Count: > 0 })
        {
            var missingScopes = requiredScopes
                .Where(scope => !validation.Scopes.Contains(scope))
                .ToList();
            if (missingScopes.Count > 0)
            {
                return TrustedServiceInvocationResult.Failure(
                    $"Service token is missing required scope(s): {string.Join(", ", missingScopes)}.",
                    403);
            }
        }

        if (allowedCallers is { Count: > 0 } &&
            !allowedCallers.Any(caller =>
                string.Equals(caller, validation.CallerClientId, StringComparison.OrdinalIgnoreCase)))
        {
            return TrustedServiceInvocationResult.Failure("Service caller is not allowed.", 403);
        }

        if (requireTenant && (metadata.TenantId is not { } || metadata.TenantId == Guid.Empty))
            return TrustedServiceInvocationResult.Failure("Tenant metadata is required.");

        return TrustedServiceInvocationResult.Success(new TrustedServiceInvocation(
            validation.CallerClientId!,
            validation.Audience!,
            metadata.TenantId,
            metadata.CredentialId,
            metadata,
            validation.Scopes));
    }
}

using XFramework.Domain.Shared.BusinessObjects;

namespace XFramework.Integration.Security;

public interface ITrustedServiceInvocationResolver
{
    Task<TrustedServiceInvocationResult> ResolveAsync(
        RequestMetadata? metadata,
        string expectedAudience,
        IReadOnlyCollection<string>? requiredScopes = null,
        IReadOnlyCollection<string>? allowedCallers = null,
        bool requireTenant = true,
        CancellationToken ct = default);
}

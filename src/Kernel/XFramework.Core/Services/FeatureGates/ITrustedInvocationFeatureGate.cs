using XFramework.Core.Patterns;

namespace XFramework.Core.Services.FeatureGates;

public interface ITrustedInvocationFeatureGate
{
    Task<Result> EnsureAllowedAsync(
        string route,
        string httpMethod,
        string? declaredCapability,
        CancellationToken ct = default);

    Task<Result> EnsureGeneratedEntityAllowedAsync(
        string authorizationFeature,
        string capability,
        bool requiresTenant,
        CancellationToken ct = default);
}

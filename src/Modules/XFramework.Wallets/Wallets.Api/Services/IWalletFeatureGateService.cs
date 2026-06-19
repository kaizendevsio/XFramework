using IdentityServer.Domain.Shared.Contracts;
using XFramework.Core.Patterns;
using XFramework.Core.Services.FeatureGates;

namespace Wallets.Api.Services;

public interface IWalletFeatureGateService
{
    Task<Result> EnsureEnabledAsync(Guid tenantId, string featureKey, CancellationToken ct = default);
}

public sealed class WalletFeatureGateService(ITenantModuleFeatureService tenantFeatureService) : IWalletFeatureGateService
{
    public Task<Result> EnsureEnabledAsync(Guid tenantId, string featureKey, CancellationToken ct = default)
    {
        var (moduleKey, subFeatureKey) = TenantModuleFeatureKeys.Normalize(featureKey);
        return tenantFeatureService.EnsureEnabledAsync(tenantId, moduleKey, subFeatureKey, ct);
    }
}

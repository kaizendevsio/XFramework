using FluentValidation;
using Microsoft.EntityFrameworkCore;
using XFramework.Core.DataContext;
using XFramework.Core.Services.FeatureGates;

namespace IdentityServer.Api.Features.GeneratedEntityValidation;

// Generated create/update and remote mutations use RegistryConfigurationValidator.
// Generated deletes skip entity validation, so protect that entry point explicitly.
public sealed class ProtectedRegistryConfigurationService(
    DbContext context,
    ITrustedInvocationContextAccessor invocation,
    GeneratedEntityAuthorizationPolicyRegistry policies,
    ITrustedInvocationFeatureGate featureGate,
    IEnumerable<IValidator<RegistryConfiguration>> validators,
    ILogger<RegistryConfigurationService> logger)
    : RegistryConfigurationService(context, invocation, policies, featureGate, validators, logger)
{
    public override async Task<Result> DeleteAsync(Guid id, Guid expectedConcurrencyStamp, CancellationToken ct = default)
    {
        if (!policies.TryGet(nameof(RegistryConfiguration), GeneratedEntityOperation.Delete, out var policy))
            return Result.Forbidden("Generated entity authorization policy is unavailable.");
        var authorization = GeneratedEntityAuthorizationEvaluator.Evaluate(invocation.Current, policy);
        if (!authorization.IsSuccess)
            return Result.Failure(authorization.Error!, authorization.StatusCode);
        var separator = policy.RequiredCapability?.LastIndexOf(':') ?? -1;
        var feature = await featureGate.EnsureGeneratedEntityAllowedAsync(
            policy.AuthorizationFeature ?? string.Empty,
            separator >= 0 ? policy.RequiredCapability![(separator + 1)..] : string.Empty,
            policy.TenantAccessMode != TenantAccessMode.Tenantless, ct);
        if (!feature.IsSuccess)
            return feature;

        var tenantId = invocation.Current?.EffectiveTenantId;
        if (tenantId is null || tenantId == Guid.Empty)
            return Result.Unauthorized("A valid tenant context is required.");
        var key = await context.Set<RegistryConfiguration>().AsNoTracking()
            .Where(x => x.Id == id && x.TenantId == tenantId)
            .Select(x => x.Key).FirstOrDefaultAsync(ct);
        if (ProtectedRegistryKeys.IsProtected(key))
            return Result.Failure(ProtectedRegistryKeys.MutationError, 400);

        return await base.DeleteAsync(id, expectedConcurrencyStamp, ct);
    }
}

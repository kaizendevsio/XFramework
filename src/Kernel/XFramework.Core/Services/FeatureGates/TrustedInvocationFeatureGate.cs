using IdentityServer.Domain.Shared.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.Security;
using XFramework.Integration.Security;

namespace XFramework.Core.Services.FeatureGates;

public sealed class TrustedInvocationFeatureGate(
    TenantModuleFeatureGateOptions options,
    ITenantModuleFeatureService featureService,
    ITenantCredentialCapabilityService capabilityService,
    ITrustedInvocationContextAccessor trustedInvocationContextAccessor,
    ILogger<TrustedInvocationFeatureGate> logger)
    : ITrustedInvocationFeatureGate
{
    public async Task<Result> EnsureAllowedAsync(
        string route,
        string httpMethod,
        string? declaredCapability,
        CancellationToken ct = default)
    {
        var normalizedRoute = route.StartsWith("/", StringComparison.Ordinal)
            ? route
            : $"/{route}";
        var rule = options.Rules
            .OrderByDescending(static item => item.PathPrefix.Length)
            .FirstOrDefault(item => item.Matches(new PathString(normalizedRoute)));
        if (rule is null)
            return Result.Success();

        var context = trustedInvocationContextAccessor.Current;
        if (context?.EffectiveTenantId is not { } tenantId || tenantId == Guid.Empty)
            return Result.Forbidden("Feature gate requires a trusted tenant context.");

        var featureResult = await featureService.EnsureEnabledAsync(
            tenantId,
            rule.ModuleKey,
            rule.SubFeatureKey,
            ct);
        if (!featureResult.IsSuccess)
        {
            LogDenial(normalizedRoute, tenantId, rule, null, featureResult.Message);
            return featureResult;
        }

        if (context.Actor is not { } actor)
            return Result.Success();

        // Cross-tenant delegation has already been authorized against the actor's
        // validated home-tenant capabilities at the invocation boundary. The actor
        // is not expected to have a credential row in the target tenant.
        if (actor.TenantId != tenantId)
            return Result.Success();

        var capability = ResolveCapability(httpMethod, normalizedRoute, declaredCapability);
        var capabilityResult = await capabilityService.EnsureAllowedAsync(
            tenantId,
            actor.CredentialId,
            rule.ModuleKey,
            rule.SubFeatureKey,
            capability,
            ct);
        if (!capabilityResult.IsSuccess)
            LogDenial(normalizedRoute, tenantId, rule, actor.CredentialId, capabilityResult.Message);

        return capabilityResult;
    }

    private void LogDenial(
        string route,
        Guid tenantId,
        TenantModuleFeatureGateRule rule,
        Guid? credentialId,
        string? message) =>
        logger.LogWarning(
            "Trusted invocation feature gate denied route {Route} for tenant {TenantId}, credential {CredentialId}, feature {FeatureKey}: {Message}",
            route,
            tenantId,
            credentialId,
            rule.FeatureKey,
            message);

    internal static string ResolveCapability(
        string httpMethod,
        string route,
        string? declaredCapability)
    {
        if (!string.IsNullOrWhiteSpace(declaredCapability))
            return declaredCapability;

        if (HttpMethods.IsGet(httpMethod) ||
            HttpMethods.IsHead(httpMethod) ||
            HttpMethods.IsOptions(httpMethod))
        {
            return IdentityAuthorizationConstants.View;
        }

        if (HttpMethods.IsDelete(httpMethod))
            return IdentityAuthorizationConstants.Delete;

        if (HttpMethods.IsPut(httpMethod) || HttpMethods.IsPatch(httpMethod))
            return IdentityAuthorizationConstants.Update;

        if (!HttpMethods.IsPost(httpMethod))
            return IdentityAuthorizationConstants.Manage;

        if (route.Contains("/query", StringComparison.OrdinalIgnoreCase) ||
            route.Contains("/search", StringComparison.OrdinalIgnoreCase) ||
            route.Contains("/get", StringComparison.OrdinalIgnoreCase) ||
            route.Contains("/reports", StringComparison.OrdinalIgnoreCase))
        {
            return IdentityAuthorizationConstants.View;
        }

        if (route.Contains("/delete", StringComparison.OrdinalIgnoreCase) ||
            route.Contains("/remove", StringComparison.OrdinalIgnoreCase))
        {
            return IdentityAuthorizationConstants.Delete;
        }

        if (route.Contains("/update", StringComparison.OrdinalIgnoreCase) ||
            route.Contains("/patch", StringComparison.OrdinalIgnoreCase) ||
            route.Contains("/replace", StringComparison.OrdinalIgnoreCase) ||
            route.Contains("/set", StringComparison.OrdinalIgnoreCase) ||
            route.Contains("/approve", StringComparison.OrdinalIgnoreCase) ||
            route.Contains("/reject", StringComparison.OrdinalIgnoreCase) ||
            route.Contains("/settle", StringComparison.OrdinalIgnoreCase) ||
            route.Contains("/cancel", StringComparison.OrdinalIgnoreCase))
        {
            return IdentityAuthorizationConstants.Update;
        }

        return IdentityAuthorizationConstants.Create;
    }
}

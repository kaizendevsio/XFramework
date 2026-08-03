using IdentityServer.Domain.Shared.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using XFramework.Core.Patterns;
using XFramework.Core.Services.FeatureGates;

namespace XFramework.Core.Middlewares;

public sealed class TenantModuleFeatureGateMiddleware(
    RequestDelegate next,
    TenantModuleFeatureGateOptions options,
    ILogger<TenantModuleFeatureGateMiddleware> logger)
{
    public async Task InvokeAsync(
        HttpContext context,
        ITenantModuleFeatureService featureService,
        ITenantCredentialCapabilityService capabilityService,
        IConfiguration configuration)
    {
        var rule = options.Rules
            .OrderByDescending(item => item.PathPrefix.Length)
            .FirstOrDefault(item => item.Matches(context.Request.Path));

        if (rule is null)
        {
            await next(context);
            return;
        }

        var tenantId = ResolveTenantId(context, configuration);
        if (tenantId is null || tenantId == Guid.Empty)
        {
            await WriteResult(
                context,
                Result.Forbidden("Feature gate requires a tenant context."));
            return;
        }

        var result = await featureService.EnsureEnabledAsync(
            tenantId.Value,
            rule.ModuleKey,
            rule.SubFeatureKey,
            context.RequestAborted);

        if (!result.IsSuccess)
        {
            logger.LogWarning(
                "Tenant module feature gate denied request {Method} {Path} for tenant {TenantId}, feature {FeatureKey}: {Message}",
                context.Request.Method,
                context.Request.Path,
                tenantId,
                TenantModuleFeatureKeys.Combine(rule.ModuleKey, rule.SubFeatureKey),
                result.Message);

            await WriteResult(context, result);
            return;
        }

        if (context.User.Identity?.IsAuthenticated == true)
        {
            var credentialId = ResolveCredentialIdFromClaims(context);
            if (credentialId is null || credentialId == Guid.Empty)
            {
                await WriteResult(
                    context,
                    Result.Forbidden("Capability gate requires a credential context."));
                return;
            }

            var capability = ResolveCapabilityKey(context.Request);
            var capabilityResult = await capabilityService.EnsureAllowedAsync(
                tenantId.Value,
                credentialId.Value,
                rule.ModuleKey,
                rule.SubFeatureKey,
                capability,
                context.RequestAborted);

            if (!capabilityResult.IsSuccess)
            {
                logger.LogWarning(
                    "Tenant credential capability gate denied request {Method} {Path} for tenant {TenantId}, credential {CredentialId}, feature {FeatureKey}, capability {CapabilityKey}: {Message}",
                    context.Request.Method,
                    context.Request.Path,
                    tenantId,
                    credentialId,
                    TenantModuleFeatureKeys.Combine(rule.ModuleKey, rule.SubFeatureKey),
                    capability,
                    capabilityResult.Message);

                await WriteResult(context, capabilityResult);
                return;
            }
        }

        await next(context);
    }

    private static Guid? ResolveTenantId(HttpContext context, IConfiguration configuration)
    {
        var claimTenantId = ResolveTenantIdFromClaims(context);
        if (claimTenantId is not null)
            return claimTenantId;

        if (context.User.Identity?.IsAuthenticated == true)
            return null;

        foreach (var headerName in new[] { "X-Tenant-Id", "X-TenantId", "tenantId", "TenantId" })
        {
            if (context.Request.Headers.TryGetValue(headerName, out var values) &&
                Guid.TryParse(values.FirstOrDefault(), out var headerTenantId))
            {
                return headerTenantId;
            }
        }

        foreach (var queryName in new[] { "tenantId", "TenantId" })
        {
            if (context.Request.Query.TryGetValue(queryName, out var values) &&
                Guid.TryParse(values.FirstOrDefault(), out var queryTenantId))
            {
                return queryTenantId;
            }
        }

        return Guid.TryParse(configuration["Tenant:DefaultId"], out var defaultTenantId)
            ? defaultTenantId
            : null;
    }

    private static Guid? ResolveTenantIdFromClaims(HttpContext context)
    {
        foreach (var claimName in new[] { "tenant_id", "tenantId", "TenantId", "tid", "tenant" })
        {
            var claimValue = context.User.FindFirst(claimName)?.Value;
            if (Guid.TryParse(claimValue, out var claimTenantId))
                return claimTenantId;
        }

        return null;
    }

    private static Guid? ResolveCredentialIdFromClaims(HttpContext context)
    {
        foreach (var claimName in new[] { "credentialId", "credential_id", ClaimTypes.NameIdentifier, "sub" })
        {
            var claimValue = context.User.FindFirst(claimName)?.Value;
            if (Guid.TryParse(claimValue, out var credentialId))
                return credentialId;
        }

        return null;
    }

    private static string ResolveCapabilityKey(HttpRequest request)
    {
        var declaredRequirement = request.HttpContext.GetEndpoint()?
            .Metadata.GetMetadata<TenantCapabilityRequirement>();
        if (declaredRequirement is not null)
            return declaredRequirement.CapabilityKey;

        var method = request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method))
            return IdentityAuthorizationConstants.View;

        if (HttpMethods.IsDelete(method))
            return IdentityAuthorizationConstants.Delete;

        if (HttpMethods.IsPut(method) || HttpMethods.IsPatch(method))
            return IdentityAuthorizationConstants.Update;

        if (HttpMethods.IsPost(method))
        {
            var path = request.Path.Value ?? string.Empty;
            if (path.Contains("/query", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("/search", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("/get", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("/reports", StringComparison.OrdinalIgnoreCase))
            {
                return IdentityAuthorizationConstants.View;
            }

            if (path.Contains("/delete", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("/remove", StringComparison.OrdinalIgnoreCase))
            {
                return IdentityAuthorizationConstants.Delete;
            }

            if (path.Contains("/update", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("/patch", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("/replace", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("/set", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("/approve", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("/reject", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("/settle", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("/cancel", StringComparison.OrdinalIgnoreCase))
            {
                return IdentityAuthorizationConstants.Update;
            }

            return IdentityAuthorizationConstants.Create;
        }

        return IdentityAuthorizationConstants.Manage;
    }

    private static async Task WriteResult(HttpContext context, Result result)
    {
        context.Response.StatusCode = result.StatusCode == 0
            ? StatusCodes.Status403Forbidden
            : result.StatusCode;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(result, context.RequestAborted);
    }
}

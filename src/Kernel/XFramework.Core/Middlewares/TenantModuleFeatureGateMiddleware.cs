using IdentityServer.Domain.Shared.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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
        foreach (var claimName in new[] { "tenantId", "TenantId", "tid" })
        {
            var claimValue = context.User.FindFirst(claimName)?.Value;
            if (Guid.TryParse(claimValue, out var claimTenantId))
                return claimTenantId;
        }

        return null;
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

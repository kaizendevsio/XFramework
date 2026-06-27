using System.Security.Claims;
using IdentityServer.Domain.Shared.Contracts;
using Microsoft.AspNetCore.Http;
using XFramework.Integration.Abstractions;
using XFramework.Core.Patterns;
using XFramework.Core.Services.FeatureGates;

namespace Communications.Api.Services;

public sealed record CommunicationsRequestContext(Guid CredentialId, Guid TenantId);
public sealed record CommunicationsTenantContext(
    Guid TenantId,
    Guid? CredentialId,
    bool IsTrustedInternal,
    bool IsAdmin,
    string? TrustedServiceName = null);

public interface ICommunicationsRequestContextResolver
{
    Result<CommunicationsRequestContext> Resolve(RequestMetadata? metadata);
    Result<CommunicationsTenantContext> ResolveTenant(RequestMetadata? metadata);
    Result<CommunicationsTenantContext> ResolveAdmin(RequestMetadata? metadata);
    Result<CommunicationsTenantContext> ResolveTrustedInternal(
        RequestMetadata? metadata,
        params string[] allowedServiceNames);
}

public sealed class CommunicationsRequestContextResolver : ICommunicationsRequestContextResolver
{
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly IConfiguration configuration;
    private readonly IJwtService? jwtService;
    private readonly ITenantModuleFeatureService? featureService;

    public CommunicationsRequestContextResolver(
        IHttpContextAccessor httpContextAccessor,
        IConfiguration configuration,
        IJwtService? jwtService = null,
        ITenantModuleFeatureService? featureService = null)
    {
        this.httpContextAccessor = httpContextAccessor;
        this.configuration = configuration;
        this.jwtService = jwtService;
        this.featureService = featureService;
    }

    public Result<CommunicationsRequestContext> Resolve(RequestMetadata? metadata)
    {
        var userContext = ResolveUserContext(metadata, enforceChatFeature: true);
        if (!userContext.IsSuccess)
            return userContext;

        return Result<CommunicationsRequestContext>.Success(userContext.Data!);
    }

    public Result<CommunicationsTenantContext> ResolveTenant(RequestMetadata? metadata)
    {
        var httpContext = httpContextAccessor.HttpContext;
        var user = httpContext?.User;
        var userContext = ResolveUserContext(metadata, enforceChatFeature: false);
        var isTrustedInternalRequest = IsTrustedServerMetadata(metadata);
        var trustedServiceName = isTrustedInternalRequest ? metadata?.Name?.Trim() : null;
        var tenantId = ResolveTenantId(user)
            ?? TryGetItemGuid(httpContext, "TenantId")
            ?? (userContext.IsSuccess ? (Guid?)userContext.Data!.TenantId : null)
            ?? (isTrustedInternalRequest ? metadata?.TenantId : null);

        if (metadata?.TenantId is { } suppliedTenantId &&
            suppliedTenantId != Guid.Empty &&
            tenantId.HasValue &&
            suppliedTenantId != tenantId.Value)
        {
            return Result<CommunicationsTenantContext>.Forbidden("Request tenant does not match trusted tenant context");
        }

        var credentialId = ResolveCredentialId(user)
            ?? TryGetItemGuid(httpContext, "CredentialId")
            ?? (userContext.IsSuccess ? (Guid?)userContext.Data!.CredentialId : null)
            ?? (isTrustedInternalRequest ? metadata?.CredentialId : null);

        if (metadata?.CredentialId is { } suppliedCredentialId &&
            suppliedCredentialId != Guid.Empty &&
            credentialId.HasValue &&
            suppliedCredentialId != credentialId.Value &&
            !IsAdmin(user) &&
            !isTrustedInternalRequest)
        {
            return Result<CommunicationsTenantContext>.Forbidden("Request credential does not match trusted actor context");
        }

        if (tenantId is null || tenantId == Guid.Empty)
            return Result<CommunicationsTenantContext>.Unauthorized("Authenticated tenant could not be resolved");

        var moduleFeature = EnsureFeatureEnabled(tenantId.Value, TenantModuleFeatureKeys.Communications);
        if (!moduleFeature.IsSuccess)
            return Result<CommunicationsTenantContext>.Failure(
                moduleFeature.Message ?? "Communications module is disabled for this tenant",
                moduleFeature.StatusCode);

        return Result<CommunicationsTenantContext>.Success(new(
            tenantId.Value,
            credentialId is Guid id && id != Guid.Empty ? id : null,
            isTrustedInternalRequest,
            IsAdmin(user),
            trustedServiceName));
    }

    public Result<CommunicationsTenantContext> ResolveAdmin(RequestMetadata? metadata)
    {
        var contextResult = ResolveTenant(metadata);
        if (!contextResult.IsSuccess)
            return contextResult;

        var context = contextResult.Data!;
        if (context.IsAdmin)
            return contextResult;

        if (!context.IsTrustedInternal || !IsTrustedAdminService(context.TrustedServiceName))
            return Result<CommunicationsTenantContext>.Forbidden("Communications administration requires an admin context");

        return contextResult;
    }

    public Result<CommunicationsTenantContext> ResolveTrustedInternal(
        RequestMetadata? metadata,
        params string[] allowedServiceNames)
    {
        var contextResult = ResolveTenant(metadata);
        if (!contextResult.IsSuccess)
            return contextResult;

        var context = contextResult.Data!;
        if (!context.IsTrustedInternal)
            return Result<CommunicationsTenantContext>.Forbidden("Communications operation requires a trusted internal service context");

        if (allowedServiceNames.Length > 0 &&
            !allowedServiceNames.Any(name =>
                string.Equals(name, context.TrustedServiceName, StringComparison.OrdinalIgnoreCase)))
        {
            return Result<CommunicationsTenantContext>.Forbidden("Communications operation is not authorized for this internal service");
        }

        return contextResult;
    }

    private bool IsTrustedServerMetadata(RequestMetadata? metadata)
    {
        var secret = GetTrustedMetadataSecret();
        if (IsPlaceholderSecret(secret))
            return false;

        var maxAgeMinutes = configuration.GetValue("Communications:TrustedMetadata:MaxAgeMinutes", 10);
        var maxAge = TimeSpan.FromMinutes(Math.Clamp(maxAgeMinutes, 1, 60));
        return RequestMetadataTrust.IsValid(metadata, secret, maxAge);
    }

    private string? GetTrustedMetadataSecret()
    {
        var secret = configuration["Communications:TrustedMetadata:SharedSecret"];
        return IsPlaceholderSecret(secret)
            ? configuration["BoltConfiguration:Signature"]
            : secret;
    }

    private Result<CommunicationsRequestContext> ResolveUserContext(RequestMetadata? metadata, bool enforceChatFeature)
    {
        var httpContext = httpContextAccessor.HttpContext;
        var user = httpContext?.User;
        var tenantId = ResolveTenantId(user) ?? TryGetItemGuid(httpContext, "TenantId");
        var credentialId = ResolveCredentialId(user) ?? TryGetItemGuid(httpContext, "CredentialId");

        if ((tenantId is null || credentialId is null) &&
            !string.IsNullOrWhiteSpace(metadata?.ActorAccessToken))
        {
            var tokenPrincipal = DecodeActorToken(metadata.ActorAccessToken);
            if (tokenPrincipal is not null)
            {
                tenantId ??= ResolveTenantId(tokenPrincipal);
                credentialId ??= ResolveCredentialId(tokenPrincipal);
            }
        }

        if (metadata?.TenantId is { } suppliedTenantId &&
            suppliedTenantId != Guid.Empty &&
            tenantId.HasValue &&
            suppliedTenantId != tenantId.Value)
        {
            return Result<CommunicationsRequestContext>.Forbidden("Request tenant does not match authenticated actor context");
        }

        if (metadata?.CredentialId is { } suppliedCredentialId &&
            suppliedCredentialId != Guid.Empty &&
            credentialId.HasValue &&
            suppliedCredentialId != credentialId.Value)
        {
            return Result<CommunicationsRequestContext>.Forbidden("Request credential does not match authenticated actor context");
        }

        if (tenantId is null || tenantId == Guid.Empty)
            return Result<CommunicationsRequestContext>.Unauthorized("Authenticated tenant could not be resolved");

        if (credentialId is null || credentialId == Guid.Empty)
            return Result<CommunicationsRequestContext>.Unauthorized("Authenticated credential could not be resolved");

        if (enforceChatFeature)
        {
            var chatFeature = EnsureFeatureEnabled(tenantId.Value, TenantModuleFeatureKeys.CommunicationsChat);
            if (!chatFeature.IsSuccess)
                return Result<CommunicationsRequestContext>.Failure(
                    chatFeature.Message ?? "Communications chat is disabled for this tenant",
                    chatFeature.StatusCode);
        }

        return Result<CommunicationsRequestContext>.Success(new(credentialId.Value, tenantId.Value));
    }

    private Result EnsureFeatureEnabled(Guid tenantId, string featureKey)
    {
        if (featureService is null)
            return Result.Success();

        try
        {
            return featureService
                .EnsureEnabledAsync(tenantId, featureKey)
                .GetAwaiter()
                .GetResult();
        }
        catch
        {
            return Result.Failure("Communications feature gate could not be evaluated", 503);
        }
    }

    private ClaimsPrincipal? DecodeActorToken(string token)
    {
        if (jwtService is null)
            return null;

        try
        {
            return jwtService.DecodeJwtToken(token).GetAwaiter().GetResult().Item1;
        }
        catch
        {
            return null;
        }
    }

    private static Guid? ResolveCredentialId(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
            return null;

        return ParseClaim(
            user,
            "credential_id",
            ClaimTypes.Name,
            ClaimTypes.NameIdentifier,
            "credentialId",
            "CredentialId",
            "sub");
    }

    private static Guid? ResolveTenantId(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
            return null;

        return ParseClaim(
            user,
            "tenant_id",
            "tenantId",
            "TenantId",
            "tid",
            "tenant");
    }

    private static Guid? ParseClaim(ClaimsPrincipal user, params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = user.Claims.FirstOrDefault(c =>
                string.Equals(c.Type, claimType, StringComparison.OrdinalIgnoreCase))?.Value;
            if (Guid.TryParse(value, out var id))
                return id;
        }

        return null;
    }

    private static Guid? TryGetItemGuid(HttpContext? context, string key)
    {
        if (context?.Items.TryGetValue(key, out var value) != true)
            return null;

        return value switch
        {
            Guid id => id,
            string text when Guid.TryParse(text, out var id) => id,
            _ => null
        };
    }

    private static bool IsAdmin(ClaimsPrincipal? user) =>
        user?.IsInRole("Admin") == true || user?.IsInRole("SuperAdmin") == true;

    private bool IsTrustedAdminService(string? serviceName)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
            return false;

        var configured = configuration["Communications:TrustedMetadata:AdminServiceNames"];
        var allowed = string.IsNullOrWhiteSpace(configured)
            ? ["ControlPanel"]
            : configured
                .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return allowed.Any(name => string.Equals(name, serviceName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsPlaceholderSecret(string? secret) =>
        string.IsNullOrWhiteSpace(secret) ||
        secret.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase);
}

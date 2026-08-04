using System.Security.Claims;
using IdentityServer.Domain.Shared.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using XFramework.Domain.Shared.Configurations;
using XFramework.Integration.Abstractions;
using XFramework.Core.Patterns;
using XFramework.Core.Services.FeatureGates;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Security;

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
    Task<Result<CommunicationsRequestContext>> ResolveAsync(
        RequestMetadata? metadata,
        CancellationToken ct = default);
    Task<Result<CommunicationsTenantContext>> ResolveTenantAsync(
        RequestMetadata? metadata,
        CancellationToken ct = default);
    Task<Result<CommunicationsTenantContext>> ResolveAdminAsync(
        RequestMetadata? metadata,
        CancellationToken ct = default);
    Task<Result<CommunicationsTenantContext>> ResolveTrustedInternalAsync(
        RequestMetadata? metadata,
        IReadOnlyCollection<string>? allowedServiceNames = null,
        CancellationToken ct = default);
}

public sealed class CommunicationsRequestContextResolver : ICommunicationsRequestContextResolver
{
    private readonly IConfiguration configuration;
    private readonly ITenantModuleFeatureService? featureService;
    private readonly ITrustedInvocationContextAccessor? trustedInvocationContextAccessor;

    public CommunicationsRequestContextResolver(
        IHttpContextAccessor httpContextAccessor,
        IConfiguration configuration,
        IJwtService? jwtService = null,
        ITenantModuleFeatureService? featureService = null,
        ITrustedInvocationContextAccessor? serviceInvocationResolver = null)
    {
        this.configuration = configuration;
        trustedInvocationContextAccessor = serviceInvocationResolver;
        this.featureService = featureService;
    }

    public async Task<Result<CommunicationsRequestContext>> ResolveAsync(
        RequestMetadata? metadata,
        CancellationToken ct = default)
    {
        var userContext = await ResolveUserContextAsync(metadata, enforceChatFeature: true, ct);
        if (!userContext.IsSuccess)
            return userContext;

        return Result<CommunicationsRequestContext>.Success(userContext.Data!);
    }

    public async Task<Result<CommunicationsTenantContext>> ResolveTenantAsync(
        RequestMetadata? metadata,
        CancellationToken ct = default)
    {
        var userContext = await ResolveUserContextAsync(metadata, enforceChatFeature: false, ct);
        var invocation = trustedInvocationContextAccessor?.Current;
        var isTrustedInternalRequest = invocation is { Actor: null, Service: not null };
        var trustedServiceName = invocation?.Service?.ClientId;
        var tenantId = invocation?.EffectiveTenantId;

        if (metadata?.RequestedTenantId is { } suppliedTenantId &&
            suppliedTenantId != Guid.Empty &&
            tenantId.HasValue &&
            suppliedTenantId != tenantId.Value)
        {
            return Result<CommunicationsTenantContext>.Forbidden("Request tenant does not match trusted tenant context");
        }

        var credentialId = invocation?.Actor?.CredentialId;

        if (tenantId is null || tenantId == Guid.Empty)
            return Result<CommunicationsTenantContext>.Unauthorized("Authenticated tenant could not be resolved");

        var moduleFeature = await EnsureFeatureEnabledAsync(
            tenantId.Value,
            TenantModuleFeatureKeys.Communications,
            ct);
        if (!moduleFeature.IsSuccess)
            return Result<CommunicationsTenantContext>.Failure(
                moduleFeature.Message ?? "Communications module is disabled for this tenant",
                moduleFeature.StatusCode);

        return Result<CommunicationsTenantContext>.Success(new(
            tenantId.Value,
            credentialId is Guid id && id != Guid.Empty ? id : null,
            isTrustedInternalRequest,
            invocation?.Actor?.Roles.Contains("Admin") == true ||
            invocation?.Actor?.Roles.Contains("SuperAdmin") == true,
            trustedServiceName));
    }

    public async Task<Result<CommunicationsTenantContext>> ResolveAdminAsync(
        RequestMetadata? metadata,
        CancellationToken ct = default)
    {
        var contextResult = await ResolveTenantAsync(metadata, ct);
        if (!contextResult.IsSuccess)
            return contextResult;

        var context = contextResult.Data!;
        if (context.IsAdmin)
            return contextResult;

        if (!context.IsTrustedInternal || !IsTrustedAdminService(context.TrustedServiceName))
            return Result<CommunicationsTenantContext>.Forbidden("Communications administration requires an admin context");

        return contextResult;
    }

    public async Task<Result<CommunicationsTenantContext>> ResolveTrustedInternalAsync(
        RequestMetadata? metadata,
        IReadOnlyCollection<string>? allowedServiceNames = null,
        CancellationToken ct = default)
    {
        var contextResult = await ResolveTenantAsync(metadata, ct);
        if (!contextResult.IsSuccess)
            return contextResult;

        var context = contextResult.Data!;
        if (!context.IsTrustedInternal)
            return Result<CommunicationsTenantContext>.Forbidden("Communications operation requires a trusted internal service context");

        if (allowedServiceNames is { Count: > 0 } &&
            !allowedServiceNames.Any(name =>
                string.Equals(name, context.TrustedServiceName, StringComparison.OrdinalIgnoreCase)))
        {
            return Result<CommunicationsTenantContext>.Forbidden("Communications operation is not authorized for this internal service");
        }

        return contextResult;
    }

    private async Task<Result<CommunicationsRequestContext>> ResolveUserContextAsync(
        RequestMetadata? metadata,
        bool enforceChatFeature,
        CancellationToken ct)
    {
        var invocation = trustedInvocationContextAccessor?.Current;
        var tenantId = invocation?.EffectiveTenantId;
        var credentialId = invocation?.Actor?.CredentialId;

        if (metadata?.RequestedTenantId is { } suppliedTenantId &&
            suppliedTenantId != Guid.Empty &&
            tenantId.HasValue &&
            suppliedTenantId != tenantId.Value)
        {
            return Result<CommunicationsRequestContext>.Forbidden("Request tenant does not match authenticated actor context");
        }

        if (tenantId is null || tenantId == Guid.Empty)
            return Result<CommunicationsRequestContext>.Unauthorized("Authenticated tenant could not be resolved");

        if (credentialId is null || credentialId == Guid.Empty)
            return Result<CommunicationsRequestContext>.Unauthorized("Authenticated credential could not be resolved");

        if (enforceChatFeature)
        {
            var chatFeature = await EnsureFeatureEnabledAsync(
                tenantId.Value,
                TenantModuleFeatureKeys.CommunicationsChat,
                ct);
            if (!chatFeature.IsSuccess)
                return Result<CommunicationsRequestContext>.Failure(
                    chatFeature.Message ?? "Communications chat is disabled for this tenant",
                    chatFeature.StatusCode);
        }

        return Result<CommunicationsRequestContext>.Success(new(credentialId.Value, tenantId.Value));
    }

    private async Task<Result> EnsureFeatureEnabledAsync(
        Guid tenantId,
        string featureKey,
        CancellationToken ct)
    {
        if (featureService is null)
            return Result.Success();

        try
        {
            return await featureService.EnsureEnabledAsync(tenantId, featureKey, ct: ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Result.Failure("Communications feature gate could not be evaluated", 503);
        }
    }

    private bool IsTrustedAdminService(string? serviceName)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
            return false;

        var configured = configuration["Communications:TrustedAdminServiceNames"];
        var allowed = string.IsNullOrWhiteSpace(configured)
            ? [XFrameworkServiceNames.Portal]
            : configured
                .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return allowed.Any(name => string.Equals(name, serviceName, StringComparison.OrdinalIgnoreCase));
    }

}

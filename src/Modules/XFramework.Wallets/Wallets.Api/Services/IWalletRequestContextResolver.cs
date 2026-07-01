using System.Security.Claims;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Security;

namespace Wallets.Api.Services;

public sealed record WalletRequestContext(
    Guid TenantId,
    Guid? ActorCredentialId,
    string? CorrelationId,
    string? IpAddress,
    string? UserAgent,
    bool IsPrivilegedActor,
    bool IsSystemActor = false);

public interface IWalletRequestContextResolver
{
    Result<WalletRequestContext> Resolve(RequestBase request, Guid? requestCredentialId = null);
}

public sealed class WalletRequestContextResolver(
    IHttpContextAccessor httpContextAccessor,
    IConfiguration configuration,
    ITrustedServiceInvocationResolver? serviceInvocationResolver = null)
    : IWalletRequestContextResolver
{
    public Result<WalletRequestContext> Resolve(RequestBase request, Guid? requestCredentialId = null)
    {
        var httpContext = httpContextAccessor.HttpContext;
        var trustedInvocation = httpContext is null ? ResolveTrustedServerMetadata(request.Metadata) : null;
        var isSignedInternalRequest = trustedInvocation is not null;
        var trustedTenantId = TryGetClaimGuid(httpContext?.User, "tenant_id", "tenantId", "TenantId", "tenant")
            ?? TryGetItemGuid(httpContext, "TenantId")
            ?? (isSignedInternalRequest ? request.Metadata.TenantId : null);

        if (trustedTenantId is null || trustedTenantId.Value == Guid.Empty)
        {
            return Result<WalletRequestContext>.Failure("Tenant context is required", 400);
        }

        if (request.Metadata.TenantId is { } suppliedTenantId &&
            suppliedTenantId != Guid.Empty &&
            suppliedTenantId != trustedTenantId.Value)
        {
            return Result<WalletRequestContext>.Forbidden("Request tenant does not match trusted tenant context");
        }

        var isSystemActor = isSignedInternalRequest || TryGetItemBool(httpContext, "WalletsPrivilegedActor");
        var isPrivilegedActor = isSystemActor || IsAdmin(httpContext?.User);
        var actorCredentialId = TryGetClaimGuid(httpContext?.User, "credential_id", "credentialId", "CredentialId", ClaimTypes.NameIdentifier)
            ?? TryGetItemGuid(httpContext, "CredentialId")
            ?? (isSignedInternalRequest ? request.Metadata.CredentialId : null);

        if (request.Metadata.CredentialId is { } metadataCredentialId &&
            metadataCredentialId != Guid.Empty &&
            actorCredentialId.HasValue &&
            metadataCredentialId != actorCredentialId.Value &&
            !isPrivilegedActor)
        {
            return Result<WalletRequestContext>.Forbidden("Request credential does not match trusted actor context");
        }

        if (requestCredentialId.HasValue &&
            actorCredentialId is null &&
            !isPrivilegedActor)
        {
            return Result<WalletRequestContext>.Forbidden("Actor credential is required for target credential operations");
        }

        if (requestCredentialId is { } targetCredentialId &&
            actorCredentialId is { } actorId &&
            actorId != targetCredentialId &&
            !isPrivilegedActor)
        {
            return Result<WalletRequestContext>.Forbidden("Actor cannot operate on the requested credential");
        }

        var ipAddress = httpContext?.Connection.RemoteIpAddress?.ToString();
        if (string.IsNullOrWhiteSpace(ipAddress) && isSignedInternalRequest)
        {
            ipAddress = request.Metadata.IpAddress;
        }

        var userAgent = httpContext?.Request.Headers.UserAgent.ToString();
        if (string.IsNullOrWhiteSpace(userAgent) && isSignedInternalRequest)
        {
            userAgent = request.Metadata.DeviceAgent;
        }

        return Result<WalletRequestContext>.Success(new WalletRequestContext(
            trustedTenantId.Value,
            actorCredentialId,
            request.Metadata.RequestId?.ToString(),
            ipAddress,
            userAgent,
            isPrivilegedActor,
            isSystemActor));
    }

    private TrustedServiceInvocation? ResolveTrustedServerMetadata(RequestMetadata? metadata)
    {
        if (serviceInvocationResolver is null)
            return null;

        var result = serviceInvocationResolver.ResolveAsync(
                metadata,
                configuration["BoltConfiguration:ClientName"] ?? XFrameworkServiceNames.Wallets,
                [XFrameworkServiceScopes.BoltService],
                requireTenant: true)
            .GetAwaiter()
            .GetResult();

        return result.IsSuccess ? result.Invocation : null;
    }

    private static bool IsAdmin(ClaimsPrincipal? user) =>
        user?.IsInRole("Admin") == true || user?.IsInRole("SuperAdmin") == true;

    private static Guid? TryGetClaimGuid(ClaimsPrincipal? user, params string[] claimTypes)
    {
        if (user is null)
        {
            return null;
        }

        foreach (var claimType in claimTypes)
        {
            var value = user.Claims.FirstOrDefault(c =>
                string.Equals(c.Type, claimType, StringComparison.OrdinalIgnoreCase))?.Value;
            if (Guid.TryParse(value, out var id))
            {
                return id;
            }
        }

        return null;
    }

    private static Guid? TryGetItemGuid(HttpContext? context, string key)
    {
        if (context?.Items.TryGetValue(key, out var value) != true)
        {
            return null;
        }

        return value switch
        {
            Guid id => id,
            string text when Guid.TryParse(text, out var id) => id,
            _ => null
        };
    }

    private static bool TryGetItemBool(HttpContext? context, string key)
    {
        if (context?.Items.TryGetValue(key, out var value) != true)
        {
            return false;
        }

        return value switch
        {
            bool flag => flag,
            string text when bool.TryParse(text, out var flag) => flag,
            _ => false
        };
    }
}

using System.Security.Claims;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.Contracts.Requests;

namespace Wallets.Api.Services;

public sealed record WalletRequestContext(
    Guid TenantId,
    Guid? ActorCredentialId,
    string? CorrelationId,
    string? IpAddress,
    string? UserAgent);

public interface IWalletRequestContextResolver
{
    Result<WalletRequestContext> Resolve(RequestBase request, Guid? requestCredentialId = null);
}

public sealed class WalletRequestContextResolver(IHttpContextAccessor httpContextAccessor)
    : IWalletRequestContextResolver
{
    public Result<WalletRequestContext> Resolve(RequestBase request, Guid? requestCredentialId = null)
    {
        var httpContext = httpContextAccessor.HttpContext;
        var trustedTenantId = TryGetClaimGuid(httpContext?.User, "tenant_id", "tenantId", "TenantId", "tenant")
            ?? TryGetItemGuid(httpContext, "TenantId")
            ?? (httpContext is null ? request.Metadata.TenantId : null);

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

        var actorCredentialId = TryGetClaimGuid(httpContext?.User, "credential_id", "credentialId", "CredentialId", ClaimTypes.NameIdentifier)
            ?? TryGetItemGuid(httpContext, "CredentialId")
            ?? (httpContext is null ? request.Metadata.CredentialId : null)
            ?? requestCredentialId;

        if (request.Metadata.CredentialId is { } metadataCredentialId &&
            metadataCredentialId != Guid.Empty &&
            actorCredentialId.HasValue &&
            metadataCredentialId != actorCredentialId.Value &&
            !IsAdmin(httpContext?.User))
        {
            return Result<WalletRequestContext>.Forbidden("Request credential does not match trusted actor context");
        }

        if (requestCredentialId is { } targetCredentialId &&
            actorCredentialId is { } actorId &&
            actorId != targetCredentialId &&
            !IsAdmin(httpContext?.User))
        {
            return Result<WalletRequestContext>.Forbidden("Actor cannot operate on the requested credential");
        }

        return Result<WalletRequestContext>.Success(new WalletRequestContext(
            trustedTenantId.Value,
            actorCredentialId,
            request.Metadata.RequestId?.ToString(),
            request.Metadata.IpAddress ?? httpContext?.Connection.RemoteIpAddress?.ToString(),
            request.Metadata.DeviceAgent ?? httpContext?.Request.Headers.UserAgent.ToString()));
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
}

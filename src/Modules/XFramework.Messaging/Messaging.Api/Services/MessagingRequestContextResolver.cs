using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using XFramework.Core.Patterns;

namespace Messaging.Api.Services;

public sealed record MessagingRequestContext(Guid CredentialId, Guid TenantId);

public interface IMessagingRequestContextResolver
{
    Result<MessagingRequestContext> Resolve(RequestMetadata? metadata);
}

public sealed class MessagingRequestContextResolver(IHttpContextAccessor httpContextAccessor)
    : IMessagingRequestContextResolver
{
    public Result<MessagingRequestContext> Resolve(RequestMetadata? metadata)
    {
        var user = httpContextAccessor.HttpContext?.User;
        var isAuthenticatedHttpRequest = user?.Identity?.IsAuthenticated == true;
        var credentialId = isAuthenticatedHttpRequest
            ? ResolveCredentialId(user)
            : metadata?.CredentialId;
        var tenantId = ResolveTenantId(user) ?? metadata?.TenantId;

        if (credentialId is null || credentialId == Guid.Empty)
            return Result<MessagingRequestContext>.Unauthorized("Authenticated credential could not be resolved");

        if (tenantId is null || tenantId == Guid.Empty)
            return Result<MessagingRequestContext>.Unauthorized("Authenticated tenant could not be resolved");

        return Result<MessagingRequestContext>.Success(new(credentialId.Value, tenantId.Value));
    }

    private static Guid? ResolveCredentialId(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
            return null;

        return ParseClaim(
            user,
            ClaimTypes.Name,
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
            "tenantId",
            "TenantId",
            "tid");
    }

    private static Guid? ParseClaim(ClaimsPrincipal user, params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = user.FindFirst(claimType)?.Value;
            if (Guid.TryParse(value, out var id))
                return id;
        }

        return null;
    }
}

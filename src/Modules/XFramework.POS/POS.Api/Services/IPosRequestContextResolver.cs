using System.Security.Claims;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Security;

namespace POS.Api.Services;

public sealed record PosRequestContext(
    Guid TenantId,
    Guid? ActorCredentialId,
    RequestMetadata Metadata,
    bool IsPrivilegedActor,
    bool IsTrustedInternal,
    string? TrustedServiceName = null);

public interface IPosRequestContextResolver
{
    Result<PosRequestContext> Resolve(RequestBase request, Guid? requestCredentialId = null);
}

public sealed class PosRequestContextResolver(
    IHttpContextAccessor httpContextAccessor,
    IConfiguration configuration,
    ITrustedServiceInvocationResolver? serviceInvocationResolver = null)
    : IPosRequestContextResolver
{
    public Result<PosRequestContext> Resolve(RequestBase request, Guid? requestCredentialId = null)
    {
        request.Metadata ??= new RequestMetadata();

        var httpContext = httpContextAccessor.HttpContext;
        var user = httpContext?.User;
        var isAuthenticatedUser = user?.Identity?.IsAuthenticated == true;
        var trustedInvocation = isAuthenticatedUser ? null : ResolveTrustedServerMetadata(request.Metadata);
        var isTrustedInternal = trustedInvocation is not null;

        var tenantId = TryGetClaimGuid(user, "tenant_id", "tenantId", "TenantId", "tid", "tenant")
            ?? TryGetItemGuid(httpContext, "TenantId")
            ?? trustedInvocation?.TenantId;

        if (tenantId is null || tenantId == Guid.Empty)
            return Result<PosRequestContext>.Failure("Tenant context is required", 400);

        if (request.Metadata.TenantId is { } suppliedTenantId &&
            suppliedTenantId != Guid.Empty &&
            suppliedTenantId != tenantId.Value)
        {
            return Result<PosRequestContext>.Forbidden("Request tenant does not match trusted tenant context");
        }

        var actorCredentialId = TryGetClaimGuid(user, "credential_id", "credentialId", "CredentialId", ClaimTypes.NameIdentifier, "sub")
            ?? TryGetItemGuid(httpContext, "CredentialId")
            ?? trustedInvocation?.ActorCredentialId
            ?? (isTrustedInternal ? request.Metadata.CredentialId : null);

        var isPrivilegedActor = isTrustedInternal || IsAdmin(user) || TryGetItemBool(httpContext, "POSPrivilegedActor");

        if (request.Metadata.CredentialId is { } suppliedCredentialId &&
            suppliedCredentialId != Guid.Empty &&
            actorCredentialId.HasValue &&
            suppliedCredentialId != actorCredentialId.Value &&
            !isPrivilegedActor)
        {
            return Result<PosRequestContext>.Forbidden("Request credential does not match trusted actor context");
        }

        if (requestCredentialId is { } targetCredentialId &&
            actorCredentialId is { } actorId &&
            actorId != targetCredentialId &&
            !isPrivilegedActor)
        {
            return Result<PosRequestContext>.Forbidden("Actor cannot operate as the requested POS credential");
        }

        if (requestCredentialId.HasValue && actorCredentialId is null && !isPrivilegedActor)
            return Result<PosRequestContext>.Forbidden("Actor credential is required for POS cashier operations");

        var sanitizedMetadata = SanitizeMetadata(request.Metadata, tenantId.Value, actorCredentialId, trustedInvocation?.Metadata);
        request.Metadata = sanitizedMetadata;

        return Result<PosRequestContext>.Success(new(
            tenantId.Value,
            actorCredentialId,
            sanitizedMetadata,
            isPrivilegedActor,
            isTrustedInternal,
            trustedInvocation?.CallerClientId));
    }

    private TrustedServiceInvocation? ResolveTrustedServerMetadata(RequestMetadata? metadata)
    {
        if (serviceInvocationResolver is null)
            return null;

        var result = serviceInvocationResolver.ResolveAsync(
                metadata,
                configuration["BoltConfiguration:ClientName"] ?? XFrameworkServiceNames.Pos,
                [XFrameworkServiceScopes.BoltService],
                requireTenant: true)
            .GetAwaiter()
            .GetResult();

        return result.IsSuccess ? result.Invocation : null;
    }

    private static RequestMetadata SanitizeMetadata(
        RequestMetadata source,
        Guid tenantId,
        Guid? actorCredentialId,
        RequestMetadata? trustedMetadata)
    {
        return new RequestMetadata
        {
            SessionId = source.SessionId ?? trustedMetadata?.SessionId,
            TenantId = tenantId,
            CredentialId = actorCredentialId ?? source.CredentialId ?? trustedMetadata?.CredentialId,
            Name = source.Name ?? trustedMetadata?.Name,
            DeviceName = source.DeviceName ?? trustedMetadata?.DeviceName,
            DeviceAgent = source.DeviceAgent ?? trustedMetadata?.DeviceAgent,
            IpAddress = source.IpAddress ?? trustedMetadata?.IpAddress,
            RequestId = source.RequestId ?? trustedMetadata?.RequestId ?? Guid.NewGuid(),
            ActorAccessToken = source.ActorAccessToken ?? trustedMetadata?.ActorAccessToken,
            ServiceAccessToken = source.ServiceAccessToken ?? trustedMetadata?.ServiceAccessToken
        };
    }

    private static Guid? TryGetClaimGuid(ClaimsPrincipal? user, params string[] claimTypes)
    {
        if (user?.Identity?.IsAuthenticated != true)
            return null;

        foreach (var claimType in claimTypes)
        {
            var value = user.Claims.FirstOrDefault(c =>
                string.Equals(c.Type, claimType, StringComparison.OrdinalIgnoreCase))?.Value;
            if (Guid.TryParse(value, out var id) && id != Guid.Empty)
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
            Guid id when id != Guid.Empty => id,
            string text when Guid.TryParse(text, out var id) && id != Guid.Empty => id,
            _ => null
        };
    }

    private static bool TryGetItemBool(HttpContext? context, string key)
    {
        if (context?.Items.TryGetValue(key, out var value) != true)
            return false;

        return value switch
        {
            bool flag => flag,
            string text when bool.TryParse(text, out var flag) => flag,
            _ => false
        };
    }

    private static bool IsAdmin(ClaimsPrincipal? user) =>
        user?.IsInRole("Admin") == true || user?.IsInRole("SuperAdmin") == true;
}

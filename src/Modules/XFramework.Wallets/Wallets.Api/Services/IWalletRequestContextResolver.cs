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
    bool IsSystemActor = false,
    IReadOnlySet<string>? ActorCapabilities = null)
{
    public bool HasCapability(string capability) =>
        IsSystemActor || ActorCapabilities?.Contains(capability) == true;
}

public interface IWalletRequestContextResolver
{
    Result<WalletRequestContext> Resolve(RequestBase request, Guid? requestCredentialId = null);
}

public sealed class WalletRequestContextResolver(
    ITrustedInvocationContextAccessor trustedInvocationContextAccessor)
    : IWalletRequestContextResolver
{
    public Result<WalletRequestContext> Resolve(RequestBase request, Guid? requestCredentialId = null)
    {
        var invocation = trustedInvocationContextAccessor.Current;
        var trustedTenantId = invocation?.EffectiveTenantId;

        if (trustedTenantId is null || trustedTenantId.Value == Guid.Empty)
        {
            return Result<WalletRequestContext>.Failure("Tenant context is required", 400);
        }

        if (request.Metadata.RequestedTenantId is { } suppliedTenantId &&
            suppliedTenantId != Guid.Empty &&
            suppliedTenantId != trustedTenantId.Value)
        {
            return Result<WalletRequestContext>.Forbidden("Request tenant does not match trusted tenant context");
        }

        var isSystemActor = invocation is { Actor: null, Service: not null };
        var isPrivilegedActor = isSystemActor ||
            invocation?.Actor?.Roles.Contains("Admin") == true ||
            invocation?.Actor?.Roles.Contains("SuperAdmin") == true;
        var actorCredentialId = invocation?.Actor?.CredentialId;

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

        return Result<WalletRequestContext>.Success(new WalletRequestContext(
            trustedTenantId.Value,
            actorCredentialId,
            request.Metadata.RequestId?.ToString(),
            request.Metadata.IpAddress,
            request.Metadata.UserAgent,
            isPrivilegedActor,
            isSystemActor,
            invocation?.Actor?.Capabilities));
    }
}

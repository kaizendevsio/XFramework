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
    ITrustedInvocationContextAccessor trustedInvocationContextAccessor)
    : IPosRequestContextResolver
{
    public Result<PosRequestContext> Resolve(RequestBase request, Guid? requestCredentialId = null)
    {
        request.Metadata ??= new RequestMetadata();
        var invocation = trustedInvocationContextAccessor.Current;
        var tenantId = invocation?.EffectiveTenantId;

        if (tenantId is null || tenantId == Guid.Empty)
            return Result<PosRequestContext>.Failure("Tenant context is required", 400);

        if (request.Metadata.RequestedTenantId is { } suppliedTenantId &&
            suppliedTenantId != Guid.Empty &&
            suppliedTenantId != tenantId.Value)
        {
            return Result<PosRequestContext>.Forbidden("Request tenant does not match trusted tenant context");
        }

        var actorCredentialId = invocation?.Actor?.CredentialId;
        var isTrustedInternal = invocation is { Actor: null, Service: not null };
        var isPrivilegedActor = isTrustedInternal ||
            invocation?.Actor?.Roles.Contains("Admin") == true ||
            invocation?.Actor?.Roles.Contains("SuperAdmin") == true;

        if (requestCredentialId is { } targetCredentialId &&
            actorCredentialId is { } actorId &&
            actorId != targetCredentialId &&
            !isPrivilegedActor)
        {
            return Result<PosRequestContext>.Forbidden("Actor cannot operate as the requested POS credential");
        }

        if (requestCredentialId.HasValue && actorCredentialId is null && !isPrivilegedActor)
            return Result<PosRequestContext>.Forbidden("Actor credential is required for POS cashier operations");

        return Result<PosRequestContext>.Success(new(
            tenantId.Value,
            actorCredentialId,
            request.Metadata,
            isPrivilegedActor,
            isTrustedInternal,
            invocation?.Service?.ClientId));
    }
}

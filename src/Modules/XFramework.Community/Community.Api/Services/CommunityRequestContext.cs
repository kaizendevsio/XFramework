using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using XFramework.Domain.Shared.DataContext;
using XFramework.Integration.Security;

namespace Community.Api.Services;

public sealed record CommunityRequester(Guid CredentialId, Guid TenantId, CommunityIdentity? Identity)
{
    public Guid IdentityId => Identity?.Id ?? Guid.Empty;
}

public interface ICommunityRequestContext
{
    Task<Result<CommunityRequester>> GetRequiredAsync(
        RequestMetadata? metadata,
        CancellationToken cancellationToken = default);

    Task<Result<CommunityRequester>> GetRequiredIdentityAsync(
        RequestMetadata? metadata,
        CancellationToken cancellationToken = default);
}

public sealed class CommunityRequestContext(
    ITrustedInvocationContextAccessor trustedInvocationContextAccessor,
    IDataContext dataContext) : ICommunityRequestContext
{
    public async Task<Result<CommunityRequester>> GetRequiredAsync(
        RequestMetadata? metadata,
        CancellationToken cancellationToken = default)
    {
        var actor = trustedInvocationContextAccessor.Current?.Actor;
        if (actor is null)
        {
            return Result<CommunityRequester>.Unauthorized("Authenticated user context is required");
        }

        if (metadata?.RequestedTenantId is { } metadataTenantId
            && metadataTenantId != Guid.Empty
            && metadataTenantId != actor.TenantId)
        {
            return Result<CommunityRequester>.Forbidden("Request tenant does not match authenticated credential tenant");
        }

        return Result<CommunityRequester>.Success(new(actor.CredentialId, actor.TenantId, null));
    }

    public async Task<Result<CommunityRequester>> GetRequiredIdentityAsync(
        RequestMetadata? metadata,
        CancellationToken cancellationToken = default)
    {
        var requesterResult = await GetRequiredAsync(metadata, cancellationToken);
        if (!requesterResult.IsSuccess)
        {
            return requesterResult;
        }

        var requester = requesterResult.Data!;
        var identity = await dataContext.Query<CommunityIdentity>()
            .Where(i => i.CredentialId == requester.CredentialId)
            .Where(i => i.TenantId == requester.TenantId)
            .Where(i => !i.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (identity is null)
        {
            return Result<CommunityRequester>.NotFound("Community identity for authenticated credential does not exist");
        }

        return Result<CommunityRequester>.Success(requester with { Identity = identity });
    }

}

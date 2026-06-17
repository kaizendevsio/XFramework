using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using XFramework.Domain.Shared.DataContext;

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
    IHttpContextAccessor httpContextAccessor,
    DbContext dbContext,
    IDataContext dataContext) : ICommunityRequestContext
{
    private static readonly string[] CredentialClaimTypes =
    [
        ClaimTypes.Name,
        ClaimTypes.NameIdentifier,
        "credentialId",
        "CredentialId",
        "sub"
    ];

    private static readonly string[] TenantClaimTypes =
    [
        "tenantId",
        "TenantId",
        "tid"
    ];

    public async Task<Result<CommunityRequester>> GetRequiredAsync(
        RequestMetadata? metadata,
        CancellationToken cancellationToken = default)
    {
        var user = httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return Result<CommunityRequester>.Unauthorized("Authenticated user context is required");
        }

        if (!TryGetClaimGuid(user, CredentialClaimTypes, out var credentialId))
        {
            return Result<CommunityRequester>.Unauthorized("Authenticated credential claim is required");
        }

        var credentialTenantId = await dbContext.Set<IdentityCredential>()
            .IgnoreQueryFilters()
            .Where(c => c.Id == credentialId && !c.IsDeleted && c.IsEnabled)
            .Select(c => c.TenantId)
            .FirstOrDefaultAsync(cancellationToken);

        if (credentialTenantId == Guid.Empty)
        {
            return Result<CommunityRequester>.Unauthorized("Authenticated credential could not be resolved");
        }

        if (TryGetClaimGuid(user, TenantClaimTypes, out var tenantClaimId)
            && tenantClaimId != credentialTenantId)
        {
            return Result<CommunityRequester>.Forbidden("Authenticated credential does not belong to the requested tenant");
        }

        if (metadata?.TenantId is { } metadataTenantId
            && metadataTenantId != Guid.Empty
            && metadataTenantId != credentialTenantId)
        {
            return Result<CommunityRequester>.Forbidden("Request tenant does not match authenticated credential tenant");
        }

        EnsureTenantClaim(user, credentialTenantId);

        return Result<CommunityRequester>.Success(new(credentialId, credentialTenantId, null));
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

    private static bool TryGetClaimGuid(
        ClaimsPrincipal user,
        IEnumerable<string> claimTypes,
        out Guid value)
    {
        foreach (var claimType in claimTypes)
        {
            var rawValue = user.FindFirst(claimType)?.Value;
            if (Guid.TryParse(rawValue, out value))
            {
                return true;
            }
        }

        value = Guid.Empty;
        return false;
    }

    private static void EnsureTenantClaim(ClaimsPrincipal user, Guid tenantId)
    {
        if (TryGetClaimGuid(user, TenantClaimTypes, out _))
        {
            return;
        }

        var identity = user.Identities.FirstOrDefault(i => i.IsAuthenticated);
        identity?.AddClaim(new("tenantId", tenantId.ToString()));
    }
}

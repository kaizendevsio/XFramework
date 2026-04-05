using Community.Domain.Shared.Contracts.Requests;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.BusinessObjects;

namespace Community.Api.Services;

/// <summary>
/// Service for managing community connections (follow/unfollow/block).
/// </summary>
public interface IConnectionService
{
    /// <summary>
    /// Creates a new connection between two community identities.
    /// </summary>
    Task<Result<CmdResponse>> CreateConnectionAsync(
        CreateConnectionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes a connection by ID, validating ownership.
    /// </summary>
    Task<Result<CmdResponse>> DeleteConnectionAsync(
        DeleteConnectionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a block connection exists between two identities in either direction.
    /// </summary>
    Task<bool> IsBlockedAsync(Guid identityA, Guid identityB, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the set of identity IDs that have an active block relationship (either direction) with the given identity.
    /// </summary>
    Task<HashSet<Guid>> GetBlockedIdentityIdsAsync(Guid identityId, CancellationToken cancellationToken = default);
}

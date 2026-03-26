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
}

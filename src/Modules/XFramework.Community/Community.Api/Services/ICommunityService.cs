namespace Community.Api.Services;

/// <summary>
/// Service for managing community operations including identity management and connections.
/// Consolidates all community operation logic previously handled by MediatR command handlers.
/// </summary>
public interface ICommunityService
{
    /// <summary>
    /// Creates a new community identity for a credential
    /// </summary>
    /// <param name="request">The create community identity request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing command response</returns>
    Task<Result<CmdResponse>> CreateCommunityIdentityAsync(
        CreateCommunityIdentityRequest request, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing community identity
    /// </summary>
    /// <param name="request">The update community identity request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing command response</returns>
    Task<Result<CmdResponse>> UpdateCommunityIdentityAsync(
        UpdateCommunityIdentityRequest request, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a list of connections for a community identity
    /// </summary>
    /// <param name="request">The get connection list request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing list of community connections</returns>
    Task<Result<List<CommunityConnection>>> GetConnectionListAsync(
        GetCommunityConnectionListRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the storage reference for an existing identity file (e.g. profile or cover photo)
    /// </summary>
    /// <param name="request">The update identity file request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing command response</returns>
    Task<Result<CmdResponse>> UpdateIdentityFileAsync(
        UpdateIdentityFileRequest request,
        CancellationToken cancellationToken = default);
}
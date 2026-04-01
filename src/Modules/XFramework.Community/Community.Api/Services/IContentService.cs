using Community.Domain.Shared.Contracts.Requests;
using Community.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.BusinessObjects;

namespace Community.Api.Services;

/// <summary>
/// Service for managing community content operations including creation, retrieval, deletion, and reactions.
/// </summary>
public interface IContentService
{
    /// <summary>
    /// Creates a new content post or comment
    /// </summary>
    Task<Result<CmdResponse>> CreateContentAsync(
        CreateContentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves content by ID with author info, reaction count, and comment count
    /// </summary>
    Task<Result<GetContentResponse>> GetContentAsync(
        GetContentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes content owned by the requester
    /// </summary>
    Task<Result<CmdResponse>> DeleteContentAsync(
        DeleteContentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Edits content owned by the requester (partial update)
    /// </summary>
    Task<Result<CmdResponse>> EditContentAsync(
        EditContentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a reaction on a content item
    /// </summary>
    Task<Result<CmdResponse>> CreateContentReactionAsync(
        CreateContentReactionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes a reaction owned by the requester
    /// </summary>
    Task<Result<CmdResponse>> DeleteContentReactionAsync(
        DeleteContentReactionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a community identity profile with follower/following/content counts
    /// </summary>
    Task<Result<GetCommunityIdentityResponse>> GetCommunityIdentityAsync(
        GetCommunityIdentityRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches community identities by HandleName or Alias with pagination
    /// </summary>
    Task<Result<PaginatedResult<SearchIdentitiesResponse>>> SearchIdentitiesAsync(
        SearchIdentitiesRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Attaches a file to content
    /// </summary>
    Task<Result<CmdResponse>> CreateContentFileAsync(
        CreateContentFileVsaRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists files attached to content
    /// </summary>
    Task<Result<List<ContentFileResponse>>> GetContentFilesAsync(
        GetContentFilesRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a file from content
    /// </summary>
    Task<Result<CmdResponse>> DeleteContentFileAsync(
        DeleteContentFileRequest request,
        CancellationToken cancellationToken = default);
}

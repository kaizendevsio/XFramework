namespace Community.Api.Services;

/// <summary>
/// Service for generating the community feed/timeline.
/// </summary>
public interface IFeedService
{
    /// <summary>
    /// Gets a paginated feed for an identity, including content from followed users and own content.
    /// </summary>
    Task<Result<GetFeedResponse>> GetFeedAsync(
        GetFeedRequest request,
        CancellationToken cancellationToken = default);
}

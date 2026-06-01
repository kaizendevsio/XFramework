using XFramework.Core.Loggers;
using XFramework.Domain.Shared.DataContext;

namespace Community.Api.Services;

/// <summary>
/// Service for generating the community feed/timeline.
/// </summary>
public sealed class FeedService : IFeedService
{
    private readonly IDataContext _dataContext;
    private readonly IConnectionService _connectionService;
    private readonly ILogger<FeedService> _logger;

    public FeedService(
        IDataContext dataContext,
        IConnectionService connectionService,
        ILogger<FeedService> logger)
    {
        _dataContext = dataContext ?? throw new ArgumentNullException(nameof(dataContext));
        _connectionService = connectionService ?? throw new ArgumentNullException(nameof(connectionService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<Result<GetFeedResponse>> GetFeedAsync(
        GetFeedRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate the identity exists
            var identity = await _dataContext.Query<CommunityIdentity>()
                .Where(i => i.Id == request.IdentityId)
                .FirstOrDefaultAsync(cancellationToken);

            if (identity == null)
            {
                _logger.CommunityIdentityNotFound(request.IdentityId);
                return Result<GetFeedResponse>.NotFound($"Identity with Id {request.IdentityId} does not exist");
            }

            // Get followed connections to extract target identity IDs
            var followedConnections = await _dataContext.Query<CommunityConnection>()
                .Where(c => c.SourceSocialMediaIdentityId == request.IdentityId)
                .Where(c => c.TypeId == Community.Domain.Shared.CommunityConnectionTypes.Follow)
                .Where(c => !c.IsDeleted)
                .Where(c => c.IsEnabled)
                .ToListAsync(cancellationToken);

            var followedIdentityIds = followedConnections
                .Select(c => c.TargetSocialMediaIdentityId)
                .ToList();

            // Include the identity's own ID to show their own content too
            var feedSourceIds = new List<Guid>(followedIdentityIds) { request.IdentityId };

            // Remove blocked identities from feed sources
            var blockedIds = await _connectionService.GetBlockedIdentityIdsAsync(request.IdentityId, cancellationToken);
            if (blockedIds.Count > 0)
            {
                feedSourceIds.RemoveAll(id => blockedIds.Contains(id));
            }

            // Get the total count for pagination
            var totalCount = await _dataContext.Query<CommunityContent>()
                .Where(c => feedSourceIds.Contains(c.SocialMediaIdentityId))
                .Where(c => !c.IsDeleted)
                .Where(c => c.IsEnabled)
                .CountAsync(cancellationToken);

            // Get the paginated feed content with includes
            var contentEntities = await _dataContext.Query<CommunityContent>()
                .Where(c => feedSourceIds.Contains(c.SocialMediaIdentityId))
                .Where(c => !c.IsDeleted)
                .Where(c => c.IsEnabled)
                .OrderByDescending(c => c.CreatedAt)
                .Skip(request.PageIndex * request.PageSize)
                .Take(request.PageSize)
                .Include(c => c.SocialMediaIdentity)
                .Include(c => c.CommunityContentReactions)
                .Include(c => c.InverseParentContent)
                .ToListAsync(cancellationToken);

            // Project to response DTOs in memory
            var feedItems = contentEntities.Select(c => new FeedItemResponse
            {
                ContentId = c.Id,
                Title = c.Title,
                Text = c.Text,
                AuthorIdentityId = c.SocialMediaIdentityId,
                AuthorHandleName = c.SocialMediaIdentity?.HandleName,
                AuthorAlias = c.SocialMediaIdentity?.Alias,
                ContentTypeId = c.TypeId,
                ReactionCount = c.CommunityContentReactions?.Count(r => !r.IsDeleted) ?? 0,
                CommentCount = c.InverseParentContent?.Count(cc => !cc.IsDeleted) ?? 0,
                CreatedAt = c.CreatedAt
            }).ToList();

            _logger.CommunityFeedRetrieved(feedItems.Count, request.IdentityId);

            return Result<GetFeedResponse>.Success(new GetFeedResponse
            {
                Items = feedItems,
                TotalCount = totalCount,
                PageIndex = request.PageIndex,
                PageSize = request.PageSize
            });
        }
        catch (Exception ex)
        {
            _logger.CommunityFeedError(request.IdentityId, ex);
            return Result<GetFeedResponse>.Failure("An error occurred while generating the feed", 500);
        }
    }
}

using Community.Domain.Shared.Contracts;
using Community.Domain.Shared.Contracts.Requests;
using Community.Domain.Shared.Contracts.Responses;
using Microsoft.Extensions.Logging;
using XFramework.Core.Loggers;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Responses;
using XFramework.Domain.Shared.DataContext;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace Community.Api.Services;

/// <summary>
/// Service for managing community content operations including creation, retrieval, deletion, and reactions.
/// </summary>
public sealed class ContentService : IContentService
{
    private readonly IDataContext _dataContext;
    private readonly ILogger<ContentService> _logger;

    public ContentService(
        IDataContext dataContext,
        ILogger<ContentService> logger)
    {
        _dataContext = dataContext ?? throw new ArgumentNullException(nameof(dataContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<Result<CmdResponse>> CreateContentAsync(
        CreateContentRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate identity exists
            var identity = await _dataContext.Query<CommunityIdentity>()
                .Where(i => i.Id == request.IdentityId && !i.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);

            if (identity == null)
            {
                _logger.CommunityIdentityNotFound(request.IdentityId);
                return Result<CmdResponse>.NotFound($"Community identity with Id {request.IdentityId} does not exist");
            }

            // If ParentContentId provided, validate parent content exists
            if (request.ParentContentId.HasValue)
            {
                var parentContent = await _dataContext.Query<CommunityContent>()
                    .Where(c => c.Id == request.ParentContentId.Value && !c.IsDeleted)
                    .FirstOrDefaultAsync(cancellationToken);

                if (parentContent == null)
                {
                    _logger.EntityNotFound("CommunityContent", request.ParentContentId.Value);
                    return Result<CmdResponse>.NotFound($"Parent content with Id {request.ParentContentId.Value} does not exist");
                }
            }

            // Create content record
            var entity = new CommunityContent
            {
                Text = request.Text,
                TypeId = request.TypeId,
                SocialMediaIdentityId = request.IdentityId,
                ParentContentId = request.ParentContentId,
                CreatedAt = DateTime.UtcNow,
                IsEnabled = true
            };

            _dataContext.Add(entity);
            await _dataContext.SaveChangesAsync(cancellationToken);

            // If it's a comment, create a notification for the content author
            if (request.ParentContentId.HasValue)
            {
                var parentContent = await _dataContext.Query<CommunityContent>()
                    .Where(c => c.Id == request.ParentContentId.Value)
                    .FirstOrDefaultAsync(cancellationToken);

                if (parentContent != null && parentContent.SocialMediaIdentityId != request.IdentityId)
                {
                    var notification = new CommunityNotification
                    {
                        RecipientIdentityId = parentContent.SocialMediaIdentityId,
                        ActorIdentityId = request.IdentityId,
                        ContentId = entity.Id,
                        Type = "Comment",
                        Message = "commented on your post",
                        CreatedAt = DateTime.UtcNow,
                        IsEnabled = true,
                        IsRead = false
                    };

                    _dataContext.Add(notification);
                    await _dataContext.SaveChangesAsync(cancellationToken);
                }
            }

            _logger.EntityCreated("CommunityContent", entity.Id);

            return Result<CmdResponse>.Success(new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.Created,
                Message = "Content created successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.OperationFailed("CreateContent", "CommunityContent", Guid.Empty, ex.Message, ex);
            return Result<CmdResponse>.Failure("An error occurred while creating content", 500);
        }
    }

    /// <inheritdoc />
    public async Task<Result<GetContentResponse>> GetContentAsync(
        GetContentRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var content = await _dataContext.Query<CommunityContent>()
                .Where(c => c.Id == request.Id && !c.IsDeleted)
                .Include(c => c.SocialMediaIdentity)
                .FirstOrDefaultAsync(cancellationToken);

            if (content == null)
            {
                _logger.EntityNotFound("CommunityContent", request.Id);
                return Result<GetContentResponse>.NotFound($"Content with Id {request.Id} does not exist");
            }

            var reactionCount = await _dataContext.Query<CommunityContentReaction>()
                .Where(r => r.ContentId == request.Id && !r.IsDeleted)
                .CountAsync(cancellationToken);

            var commentCount = await _dataContext.Query<CommunityContent>()
                .Where(c => c.ParentContentId == request.Id && !c.IsDeleted)
                .CountAsync(cancellationToken);

            var response = new GetContentResponse
            {
                Id = content.Id,
                Title = content.Title,
                Text = content.Text,
                SocialMediaIdentityId = content.SocialMediaIdentityId,
                AuthorHandleName = content.SocialMediaIdentity?.HandleName,
                AuthorAlias = content.SocialMediaIdentity?.Alias,
                TypeId = content.TypeId,
                ParentContentId = content.ParentContentId,
                ReactionCount = reactionCount,
                CommentCount = commentCount,
                CreatedAt = content.CreatedAt,
                ModifiedAt = content.ModifiedAt
            };

            _logger.EntityRetrieved("CommunityContent", request.Id);

            return Result<GetContentResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.OperationFailed("GetContent", "CommunityContent", request.Id, ex.Message, ex);
            return Result<GetContentResponse>.Failure("An error occurred while retrieving content", 500);
        }
    }

    /// <inheritdoc />
    public async Task<Result<CmdResponse>> DeleteContentAsync(
        DeleteContentRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var content = await _dataContext.Query<CommunityContent>()
                .Where(c => c.Id == request.Id && !c.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);

            if (content == null)
            {
                _logger.EntityNotFound("CommunityContent", request.Id);
                return Result<CmdResponse>.NotFound($"Content with Id {request.Id} does not exist");
            }

            // Validate requester owns the content
            if (content.SocialMediaIdentityId != request.RequesterId)
            {
                _logger.BusinessRuleViolation("DeleteContent", $"Identity {request.RequesterId} does not own content {request.Id}");
                return Result<CmdResponse>.Forbidden("You do not have permission to delete this content");
            }

            // Soft-delete
            content.IsDeleted = true;
            content.DeletedAt = DateTime.UtcNow;
            _dataContext.Update(content);
            await _dataContext.SaveChangesAsync(cancellationToken);

            _logger.EntityDeleted("CommunityContent", request.Id);

            return Result<CmdResponse>.Success(new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.OK,
                Message = "Content deleted successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.OperationFailed("DeleteContent", "CommunityContent", request.Id, ex.Message, ex);
            return Result<CmdResponse>.Failure("An error occurred while deleting content", 500);
        }
    }

    /// <inheritdoc />
    public async Task<Result<CmdResponse>> CreateContentReactionAsync(
        CreateContentReactionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate content exists
            var content = await _dataContext.Query<CommunityContent>()
                .Where(c => c.Id == request.ContentId && !c.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);

            if (content == null)
            {
                _logger.EntityNotFound("CommunityContent", request.ContentId);
                return Result<CmdResponse>.NotFound($"Content with Id {request.ContentId} does not exist");
            }

            // Validate identity exists
            var identity = await _dataContext.Query<CommunityIdentity>()
                .Where(i => i.Id == request.IdentityId && !i.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);

            if (identity == null)
            {
                _logger.CommunityIdentityNotFound(request.IdentityId);
                return Result<CmdResponse>.NotFound($"Community identity with Id {request.IdentityId} does not exist");
            }

            // Prevent duplicate (same identity + same type on same content)
            var existingReaction = await _dataContext.Query<CommunityContentReaction>()
                .Where(r => r.ContentId == request.ContentId
                         && r.SocialMediaIdentityId == request.IdentityId
                         && r.TypeId == request.TypeId
                         && !r.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);

            if (existingReaction != null)
            {
                _logger.BusinessRuleViolation("CreateContentReaction", $"Duplicate reaction: identity {request.IdentityId} already reacted with type {request.TypeId} on content {request.ContentId}");
                return Result<CmdResponse>.Conflict("You have already reacted with this type on this content");
            }

            // Create reaction
            var entity = new CommunityContentReaction
            {
                ContentId = request.ContentId,
                TypeId = request.TypeId,
                SocialMediaIdentityId = request.IdentityId,
                CreatedAt = DateTime.UtcNow,
                IsEnabled = true
            };

            _dataContext.Add(entity);
            await _dataContext.SaveChangesAsync(cancellationToken);

            // Create notification for content author (if not reacting to own content)
            if (content.SocialMediaIdentityId != request.IdentityId)
            {
                var notification = new CommunityNotification
                {
                    RecipientIdentityId = content.SocialMediaIdentityId,
                    ActorIdentityId = request.IdentityId,
                    ContentId = content.Id,
                    Type = "Reaction",
                    Message = "reacted to your post",
                    CreatedAt = DateTime.UtcNow,
                    IsEnabled = true,
                    IsRead = false
                };

                _dataContext.Add(notification);
                await _dataContext.SaveChangesAsync(cancellationToken);
            }

            _logger.EntityCreated("CommunityContentReaction", entity.Id);

            return Result<CmdResponse>.Success(new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.Created,
                Message = "Reaction created successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.OperationFailed("CreateContentReaction", "CommunityContentReaction", Guid.Empty, ex.Message, ex);
            return Result<CmdResponse>.Failure("An error occurred while creating reaction", 500);
        }
    }

    /// <inheritdoc />
    public async Task<Result<CmdResponse>> DeleteContentReactionAsync(
        DeleteContentReactionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var reaction = await _dataContext.Query<CommunityContentReaction>()
                .Where(r => r.Id == request.ReactionId && r.ContentId == request.ContentId && !r.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);

            if (reaction == null)
            {
                _logger.EntityNotFound("CommunityContentReaction", request.ReactionId);
                return Result<CmdResponse>.NotFound($"Reaction with Id {request.ReactionId} does not exist");
            }

            // Validate requester owns the reaction
            if (reaction.SocialMediaIdentityId != request.RequesterId)
            {
                _logger.BusinessRuleViolation("DeleteContentReaction", $"Identity {request.RequesterId} does not own reaction {request.ReactionId}");
                return Result<CmdResponse>.Forbidden("You do not have permission to delete this reaction");
            }

            // Soft-delete
            reaction.IsDeleted = true;
            reaction.DeletedAt = DateTime.UtcNow;
            _dataContext.Update(reaction);
            await _dataContext.SaveChangesAsync(cancellationToken);

            _logger.EntityDeleted("CommunityContentReaction", request.ReactionId);

            return Result<CmdResponse>.Success(new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.OK,
                Message = "Reaction deleted successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.OperationFailed("DeleteContentReaction", "CommunityContentReaction", request.ReactionId, ex.Message, ex);
            return Result<CmdResponse>.Failure("An error occurred while deleting reaction", 500);
        }
    }

    /// <inheritdoc />
    public async Task<Result<GetCommunityIdentityResponse>> GetCommunityIdentityAsync(
        GetCommunityIdentityRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var identity = await _dataContext.Query<CommunityIdentity>()
                .Where(i => i.Id == request.Id && !i.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);

            if (identity == null)
            {
                _logger.CommunityIdentityNotFound(request.Id);
                return Result<GetCommunityIdentityResponse>.NotFound($"Community identity with Id {request.Id} does not exist");
            }

            // Follower count: connections where this identity is the target
            var followerCount = await _dataContext.Query<CommunityConnection>()
                .Where(c => c.TargetSocialMediaIdentityId == request.Id && !c.IsDeleted)
                .CountAsync(cancellationToken);

            // Following count: connections where this identity is the source
            var followingCount = await _dataContext.Query<CommunityConnection>()
                .Where(c => c.SourceSocialMediaIdentityId == request.Id && !c.IsDeleted)
                .CountAsync(cancellationToken);

            // Content count: non-deleted content authored by this identity
            var contentCount = await _dataContext.Query<CommunityContent>()
                .Where(c => c.SocialMediaIdentityId == request.Id && !c.IsDeleted && c.ParentContentId == null)
                .CountAsync(cancellationToken);

            var response = new GetCommunityIdentityResponse
            {
                Id = identity.Id,
                HandleName = identity.HandleName,
                Tagline = identity.Tagline,
                Alias = identity.Alias,
                Status = identity.Status,
                LastActive = identity.LastActive,
                TypeId = identity.TypeId,
                FollowerCount = followerCount,
                FollowingCount = followingCount,
                ContentCount = contentCount
            };

            _logger.EntityRetrieved("CommunityIdentity", request.Id);

            return Result<GetCommunityIdentityResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.OperationFailed("GetCommunityIdentity", "CommunityIdentity", request.Id, ex.Message, ex);
            return Result<GetCommunityIdentityResponse>.Failure("An error occurred while retrieving community identity", 500);
        }
    }

    /// <inheritdoc />
    public async Task<Result<PaginatedResult<SearchIdentitiesResponse>>> SearchIdentitiesAsync(
        SearchIdentitiesRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _dataContext.Query<CommunityIdentity>()
                .Where(i => !i.IsDeleted);

            // Filter by TypeId if provided
            if (request.TypeId.HasValue)
            {
                query = query.Where(i => i.TypeId == request.TypeId.Value);
            }

            // Search by HandleName or Alias (contains match)
            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                var searchTerm = request.SearchTerm.ToLower();
                query = query.Where(i =>
                    (i.HandleName != null && i.HandleName.ToLower().Contains(searchTerm)) ||
                    (i.Alias != null && i.Alias.ToLower().Contains(searchTerm)));
            }

            var totalItems = await query.CountAsync(cancellationToken);

            var pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
            var pageSize = request.PageSize < 1 ? 20 : request.PageSize;

            var entities = await query
                .OrderBy(i => i.HandleName)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            var items = entities.Select(i => new SearchIdentitiesResponse
            {
                Id = i.Id,
                HandleName = i.HandleName,
                Alias = i.Alias,
                Tagline = i.Tagline,
                Status = i.Status,
                TypeId = i.TypeId
            }).ToList();

            _logger.EntityQueryCompleted(totalItems, "CommunityIdentity");

            var result = new PaginatedResult<SearchIdentitiesResponse>(
                totalItems,
                pageIndex,
                pageSize,
                items);

            return Result<PaginatedResult<SearchIdentitiesResponse>>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.OperationFailed("SearchIdentities", "CommunityIdentity", Guid.Empty, ex.Message, ex);
            return Result<PaginatedResult<SearchIdentitiesResponse>>.Failure("An error occurred while searching identities", 500);
        }
    }
}

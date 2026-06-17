using XFramework.Core.Loggers;
using XFramework.Domain.Shared.DataContext;

namespace Community.Api.Services;

/// <summary>
/// Service for managing community content operations including creation, retrieval, deletion, and reactions.
/// </summary>
public sealed class ContentService : IContentService
{
    private readonly IDataContext _dataContext;
    private readonly IConnectionService _connectionService;
    private readonly ICommunityRequestContext _requestContext;
    private readonly ILogger<ContentService> _logger;

    public ContentService(
        IDataContext dataContext,
        IConnectionService connectionService,
        ICommunityRequestContext requestContext,
        ILogger<ContentService> logger)
    {
        _dataContext = dataContext ?? throw new ArgumentNullException(nameof(dataContext));
        _connectionService = connectionService ?? throw new ArgumentNullException(nameof(connectionService));
        _requestContext = requestContext ?? throw new ArgumentNullException(nameof(requestContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<Result<CmdResponse>> CreateContentAsync(
        CreateContentRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var requesterResult = await _requestContext.GetRequiredIdentityAsync(request.Metadata, cancellationToken);
            if (!requesterResult.IsSuccess)
                return CommunitySecurity.ToFailure<CommunityRequester, CmdResponse>(requesterResult);

            var requester = requesterResult.Data!;
            if (CommunitySecurity.IsSpoofed(request.IdentityId, requester.IdentityId))
                return Result<CmdResponse>.Forbidden("Identity ID does not match authenticated user");

            // Validate identity exists
            var identity = await _dataContext.Query<CommunityIdentity>()
                .Where(i => i.TenantId == requester.TenantId)
                .Where(i => i.Id == requester.IdentityId && !i.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);

            if (identity == null)
            {
                _logger.CommunityIdentityNotFound(requester.IdentityId);
                return Result<CmdResponse>.NotFound($"Community identity with Id {requester.IdentityId} does not exist");
            }

            var contentTypeExists = await _dataContext.Query<CommunityContentType>()
                .Where(t => t.TenantId == requester.TenantId)
                .AnyAsync(t => t.Id == request.TypeId, cancellationToken);

            if (!contentTypeExists)
            {
                _logger.EntityNotFound("CommunityContentType", request.TypeId);
                return Result<CmdResponse>.NotFound($"Content type with Id {request.TypeId} does not exist");
            }

            // If ParentContentId provided, validate parent content exists
            CommunityContent? parentContent = null;
            if (request.ParentContentId.HasValue)
            {
                parentContent = await _dataContext.Query<CommunityContent>()
                    .Where(c => c.TenantId == requester.TenantId)
                    .Where(c => c.Id == request.ParentContentId.Value && !c.IsDeleted)
                    .FirstOrDefaultAsync(cancellationToken);

                if (parentContent == null)
                {
                    _logger.EntityNotFound("CommunityContent", request.ParentContentId.Value);
                    return Result<CmdResponse>.NotFound($"Parent content with Id {request.ParentContentId.Value} does not exist");
                }

                if (await _connectionService.IsBlockedAsync(
                        requester.IdentityId,
                        parentContent.SocialMediaIdentityId,
                        cancellationToken))
                {
                    return Result<CmdResponse>.Forbidden("Cannot comment because a block exists between you and the content author");
                }
            }

            // Create content record
            var entity = new CommunityContent
            {
                TenantId = requester.TenantId,
                Text = request.Text,
                TypeId = request.TypeId,
                SocialMediaIdentityId = requester.IdentityId,
                CommunityGroupId = parentContent?.CommunityGroupId ?? requester.IdentityId,
                ParentContentId = request.ParentContentId,
                CreatedAt = DateTime.UtcNow,
                IsEnabled = true
            };

            _dataContext.Add(entity);
            var saveResult = await _dataContext.SaveChangesAsync(cancellationToken);
            if (CommunitySecurity.SaveFailure(saveResult, "CreateContent") is { } saveFailure)
                return saveFailure;

            // If it's a comment, create a notification for the content author
            if (parentContent is not null)
            {
                if (parentContent.SocialMediaIdentityId != requester.IdentityId)
                {
                    var notification = new CommunityNotification
                    {
                        TenantId = requester.TenantId,
                        RecipientIdentityId = parentContent.SocialMediaIdentityId,
                        ActorIdentityId = requester.IdentityId,
                        ContentId = entity.Id,
                        Type = "Comment",
                        Message = "commented on your post",
                        CreatedAt = DateTime.UtcNow,
                        IsEnabled = true,
                        IsRead = false
                    };

                    _dataContext.Add(notification);
                    var notificationSaveResult = await _dataContext.SaveChangesAsync(cancellationToken);
                    if (CommunitySecurity.SaveFailure(notificationSaveResult, "CreateCommentNotification") is { } notificationSaveFailure)
                        return notificationSaveFailure;
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

            if (request.RequestingIdentityId is { } requestingIdentityId
                && requestingIdentityId != Guid.Empty
                && requestingIdentityId != content.SocialMediaIdentityId
                && await _connectionService.IsBlockedAsync(
                    requestingIdentityId,
                    content.SocialMediaIdentityId,
                    cancellationToken))
            {
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
            var requesterResult = await _requestContext.GetRequiredIdentityAsync(request.Metadata, cancellationToken);
            if (!requesterResult.IsSuccess)
                return CommunitySecurity.ToFailure<CommunityRequester, CmdResponse>(requesterResult);

            var requester = requesterResult.Data!;
            if (CommunitySecurity.IsSpoofed(request.RequesterId, requester.IdentityId))
                return Result<CmdResponse>.Forbidden("Requester ID does not match authenticated user");

            var content = await _dataContext.Query<CommunityContent>()
                .Where(c => c.TenantId == requester.TenantId)
                .Where(c => c.Id == request.Id && !c.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);

            if (content == null)
            {
                _logger.EntityNotFound("CommunityContent", request.Id);
                return Result<CmdResponse>.NotFound($"Content with Id {request.Id} does not exist");
            }

            // Validate requester owns the content
            if (content.SocialMediaIdentityId != requester.IdentityId)
            {
                _logger.BusinessRuleViolation("DeleteContent", $"Identity {requester.IdentityId} does not own content {request.Id}");
                return Result<CmdResponse>.Forbidden("You do not have permission to delete this content");
            }

            // Soft-delete
            content.IsDeleted = true;
            content.DeletedAt = DateTime.UtcNow;
            _dataContext.Update(content);
            var saveResult = await _dataContext.SaveChangesAsync(cancellationToken);
            if (CommunitySecurity.SaveFailure(saveResult, "DeleteContent") is { } saveFailure)
                return saveFailure;

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
    public async Task<Result<CmdResponse>> EditContentAsync(
        EditContentRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var requesterResult = await _requestContext.GetRequiredIdentityAsync(request.Metadata, cancellationToken);
            if (!requesterResult.IsSuccess)
                return CommunitySecurity.ToFailure<CommunityRequester, CmdResponse>(requesterResult);

            var requester = requesterResult.Data!;
            if (CommunitySecurity.IsSpoofed(request.RequestingIdentityId, requester.IdentityId))
                return Result<CmdResponse>.Forbidden("Requesting identity ID does not match authenticated user");

            var content = await _dataContext.Query<CommunityContent>()
                .Where(c => c.TenantId == requester.TenantId)
                .Where(c => c.Id == request.ContentId && !c.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);

            if (content == null)
            {
                _logger.EntityNotFound("CommunityContent", request.ContentId);
                return Result<CmdResponse>.NotFound($"Content with Id {request.ContentId} does not exist");
            }

            if (content.SocialMediaIdentityId != requester.IdentityId)
            {
                _logger.BusinessRuleViolation("EditContent", $"Identity {requester.IdentityId} does not own content {request.ContentId}");
                return Result<CmdResponse>.Forbidden("You do not have permission to edit this content");
            }

            if (request.Text is not null)
                content.Text = request.Text;

            if (request.Title is not null)
                content.Title = request.Title;

            content.ModifiedAt = DateTime.UtcNow;

            _dataContext.Update(content);
            var saveResult = await _dataContext.SaveChangesAsync(cancellationToken);
            if (CommunitySecurity.SaveFailure(saveResult, "EditContent") is { } saveFailure)
                return saveFailure;

            _logger.EntityUpdated("CommunityContent", request.ContentId);

            return Result<CmdResponse>.Success(new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.OK,
                Message = "Content updated successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.OperationFailed("EditContent", "CommunityContent", request.ContentId, ex.Message, ex);
            return Result<CmdResponse>.Failure("An error occurred while editing content", 500);
        }
    }

    /// <inheritdoc />
    public async Task<Result<CmdResponse>> CreateContentReactionAsync(
        CreateContentReactionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var requesterResult = await _requestContext.GetRequiredIdentityAsync(request.Metadata, cancellationToken);
            if (!requesterResult.IsSuccess)
                return CommunitySecurity.ToFailure<CommunityRequester, CmdResponse>(requesterResult);

            var requester = requesterResult.Data!;
            if (CommunitySecurity.IsSpoofed(request.IdentityId, requester.IdentityId))
                return Result<CmdResponse>.Forbidden("Identity ID does not match authenticated user");

            // Validate content exists
            var content = await _dataContext.Query<CommunityContent>()
                .Where(c => c.TenantId == requester.TenantId)
                .Where(c => c.Id == request.ContentId && !c.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);

            if (content == null)
            {
                _logger.EntityNotFound("CommunityContent", request.ContentId);
                return Result<CmdResponse>.NotFound($"Content with Id {request.ContentId} does not exist");
            }

            // Validate identity exists
            var identity = await _dataContext.Query<CommunityIdentity>()
                .Where(i => i.TenantId == requester.TenantId)
                .Where(i => i.Id == requester.IdentityId && !i.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);

            if (identity == null)
            {
                _logger.CommunityIdentityNotFound(requester.IdentityId);
                return Result<CmdResponse>.NotFound($"Community identity with Id {requester.IdentityId} does not exist");
            }

            var reactionTypeExists = await _dataContext.Query<CommunityContentReactionType>()
                .Where(t => t.TenantId == requester.TenantId)
                .AnyAsync(t => t.Id == request.TypeId, cancellationToken);

            if (!reactionTypeExists)
            {
                _logger.EntityNotFound("CommunityContentReactionType", request.TypeId);
                return Result<CmdResponse>.NotFound($"Reaction type with Id {request.TypeId} does not exist");
            }

            // Block check
            if (await _connectionService.IsBlockedAsync(requester.IdentityId, content.SocialMediaIdentityId, cancellationToken))
                return Result<CmdResponse>.Forbidden("Cannot react because a block exists between you and the content author");

            // Prevent duplicate (same identity + same type on same content)
            var existingReaction = await _dataContext.Query<CommunityContentReaction>()
                .Where(r => r.TenantId == requester.TenantId)
                .Where(r => r.ContentId == request.ContentId
                         && r.SocialMediaIdentityId == requester.IdentityId
                         && r.TypeId == request.TypeId
                         && !r.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);

            if (existingReaction != null)
            {
                _logger.BusinessRuleViolation("CreateContentReaction", $"Duplicate reaction: identity {requester.IdentityId} already reacted with type {request.TypeId} on content {request.ContentId}");
                return Result<CmdResponse>.Conflict("You have already reacted with this type on this content");
            }

            // Create reaction
            var entity = new CommunityContentReaction
            {
                TenantId = requester.TenantId,
                ContentId = request.ContentId,
                TypeId = request.TypeId,
                SocialMediaIdentityId = requester.IdentityId,
                CreatedAt = DateTime.UtcNow,
                IsEnabled = true
            };

            _dataContext.Add(entity);
            var saveResult = await _dataContext.SaveChangesAsync(cancellationToken);
            if (CommunitySecurity.SaveFailure(saveResult, "CreateContentReaction") is { } saveFailure)
                return saveFailure;

            // Create notification for content author (if not reacting to own content)
            if (content.SocialMediaIdentityId != requester.IdentityId)
            {
                var notification = new CommunityNotification
                {
                    TenantId = requester.TenantId,
                    RecipientIdentityId = content.SocialMediaIdentityId,
                    ActorIdentityId = requester.IdentityId,
                    ContentId = content.Id,
                    Type = "Reaction",
                    Message = "reacted to your post",
                    CreatedAt = DateTime.UtcNow,
                    IsEnabled = true,
                    IsRead = false
                };

                _dataContext.Add(notification);
                var notificationSaveResult = await _dataContext.SaveChangesAsync(cancellationToken);
                if (CommunitySecurity.SaveFailure(notificationSaveResult, "CreateReactionNotification") is { } notificationSaveFailure)
                    return notificationSaveFailure;
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
            var requesterResult = await _requestContext.GetRequiredIdentityAsync(request.Metadata, cancellationToken);
            if (!requesterResult.IsSuccess)
                return CommunitySecurity.ToFailure<CommunityRequester, CmdResponse>(requesterResult);

            var requester = requesterResult.Data!;
            if (CommunitySecurity.IsSpoofed(request.RequesterId, requester.IdentityId))
                return Result<CmdResponse>.Forbidden("Requester ID does not match authenticated user");

            var reaction = await _dataContext.Query<CommunityContentReaction>()
                .Where(r => r.TenantId == requester.TenantId)
                .Where(r => r.Id == request.ReactionId && r.ContentId == request.ContentId && !r.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);

            if (reaction == null)
            {
                _logger.EntityNotFound("CommunityContentReaction", request.ReactionId);
                return Result<CmdResponse>.NotFound($"Reaction with Id {request.ReactionId} does not exist");
            }

            // Validate requester owns the reaction
            if (reaction.SocialMediaIdentityId != requester.IdentityId)
            {
                _logger.BusinessRuleViolation("DeleteContentReaction", $"Identity {requester.IdentityId} does not own reaction {request.ReactionId}");
                return Result<CmdResponse>.Forbidden("You do not have permission to delete this reaction");
            }

            // Soft-delete
            reaction.IsDeleted = true;
            reaction.DeletedAt = DateTime.UtcNow;
            _dataContext.Update(reaction);
            var saveResult = await _dataContext.SaveChangesAsync(cancellationToken);
            if (CommunitySecurity.SaveFailure(saveResult, "DeleteContentReaction") is { } saveFailure)
                return saveFailure;

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

            if (request.RequestingIdentityId is { } requestingIdentityId
                && requestingIdentityId != Guid.Empty
                && requestingIdentityId != request.Id
                && await _connectionService.IsBlockedAsync(requestingIdentityId, request.Id, cancellationToken))
            {
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

            // Block enforcement: exclude blocked identities if a requester context is provided
            if (request.RequestingIdentityId.HasValue && request.RequestingIdentityId.Value != Guid.Empty)
            {
                var blockedIds = await _connectionService.GetBlockedIdentityIdsAsync(request.RequestingIdentityId.Value, cancellationToken);
                if (blockedIds.Count > 0)
                {
                    var blockedList = blockedIds.ToList();
                    query = query.Where(i => !blockedList.Contains(i.Id));
                }
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

    /// <inheritdoc />
    public async Task<Result<CmdResponse>> CreateContentFileAsync(
        CreateContentFileVsaRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var requesterResult = await _requestContext.GetRequiredIdentityAsync(request.Metadata, cancellationToken);
            if (!requesterResult.IsSuccess)
                return CommunitySecurity.ToFailure<CommunityRequester, CmdResponse>(requesterResult);

            var requester = requesterResult.Data!;
            if (CommunitySecurity.IsSpoofed(request.RequestingIdentityId, requester.IdentityId))
                return Result<CmdResponse>.Forbidden("Requesting identity ID does not match authenticated user");

            var content = await _dataContext.Query<CommunityContent>()
                .Where(c => c.TenantId == requester.TenantId)
                .Where(c => c.Id == request.ContentId && !c.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);

            if (content == null)
                return Result<CmdResponse>.NotFound($"Content with Id {request.ContentId} does not exist");

            if (content.SocialMediaIdentityId != requester.IdentityId)
                return Result<CmdResponse>.Forbidden("You do not have permission to attach files to this content");

            var storageFileExists = await _dataContext.Query<StorageFile>()
                .Where(f => f.TenantId == requester.TenantId)
                .AnyAsync(f => f.Id == request.StorageFileId, cancellationToken);

            if (!storageFileExists)
                return Result<CmdResponse>.NotFound($"Storage file with Id {request.StorageFileId} does not exist");

            var entity = new CommunityContentFile
            {
                TenantId = requester.TenantId,
                ContentId = request.ContentId,
                StorageId = request.StorageFileId,
                CreatedAt = DateTime.UtcNow,
                IsEnabled = true
            };

            _dataContext.Add(entity);
            var saveResult = await _dataContext.SaveChangesAsync(cancellationToken);
            if (CommunitySecurity.SaveFailure(saveResult, "CreateContentFile") is { } saveFailure)
                return saveFailure;

            return Result<CmdResponse>.Success(new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.Created,
                Message = "File attached successfully"
            }, 201);
        }
        catch (Exception ex)
        {
            _logger.OperationFailed("CreateContentFile", "CommunityContentFile", Guid.Empty, ex.Message, ex);
            return Result<CmdResponse>.Failure("An error occurred while attaching file", 500);
        }
    }

    /// <inheritdoc />
    public async Task<Result<List<ContentFileResponse>>> GetContentFilesAsync(
        GetContentFilesRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var contentExists = await _dataContext.Query<CommunityContent>()
                .Where(c => c.Id == request.ContentId && !c.IsDeleted)
                .AnyAsync(cancellationToken);

            if (!contentExists)
                return Result<List<ContentFileResponse>>.NotFound($"Content with Id {request.ContentId} does not exist");

            var files = await _dataContext.Query<CommunityContentFile>()
                .Where(f => f.ContentId == request.ContentId && !f.IsDeleted)
                .ToListAsync(cancellationToken);

            var result = files.Select(f => new ContentFileResponse
            {
                Id = f.Id,
                ContentId = f.ContentId,
                StorageFileId = f.StorageId,
                CreatedAt = f.CreatedAt
            }).ToList();

            return Result<List<ContentFileResponse>>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.OperationFailed("GetContentFiles", "CommunityContentFile", request.ContentId, ex.Message, ex);
            return Result<List<ContentFileResponse>>.Failure("An error occurred while retrieving files", 500);
        }
    }

    /// <inheritdoc />
    public async Task<Result<CmdResponse>> DeleteContentFileAsync(
        DeleteContentFileRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var requesterResult = await _requestContext.GetRequiredIdentityAsync(request.Metadata, cancellationToken);
            if (!requesterResult.IsSuccess)
                return CommunitySecurity.ToFailure<CommunityRequester, CmdResponse>(requesterResult);

            var requester = requesterResult.Data!;
            if (CommunitySecurity.IsSpoofed(request.RequestingIdentityId, requester.IdentityId))
                return Result<CmdResponse>.Forbidden("Requesting identity ID does not match authenticated user");

            var content = await _dataContext.Query<CommunityContent>()
                .Where(c => c.TenantId == requester.TenantId)
                .Where(c => c.Id == request.ContentId && !c.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);

            if (content == null)
                return Result<CmdResponse>.NotFound($"Content with Id {request.ContentId} does not exist");

            if (content.SocialMediaIdentityId != requester.IdentityId)
                return Result<CmdResponse>.Forbidden("You do not have permission to remove files from this content");

            var file = await _dataContext.Query<CommunityContentFile>()
                .Where(f => f.TenantId == requester.TenantId)
                .Where(f => f.Id == request.FileId && f.ContentId == request.ContentId && !f.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);

            if (file == null)
                return Result<CmdResponse>.NotFound($"File with Id {request.FileId} does not exist");

            file.IsDeleted = true;
            file.DeletedAt = DateTime.UtcNow;
            _dataContext.Update(file);
            var saveResult = await _dataContext.SaveChangesAsync(cancellationToken);
            if (CommunitySecurity.SaveFailure(saveResult, "DeleteContentFile") is { } saveFailure)
                return saveFailure;

            return Result<CmdResponse>.Success(new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.OK,
                Message = "File removed successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.OperationFailed("DeleteContentFile", "CommunityContentFile", request.FileId, ex.Message, ex);
            return Result<CmdResponse>.Failure("An error occurred while removing file", 500);
        }
    }
}

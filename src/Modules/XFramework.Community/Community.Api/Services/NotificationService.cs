using XFramework.Core.Loggers;
using XFramework.Domain.Shared.DataContext;

namespace Community.Api.Services;

/// <summary>
/// Service for managing community notifications.
/// </summary>
public sealed class NotificationService : INotificationService
{
    private readonly IDataContext _dataContext;
    private readonly ICommunityRequestContext _requestContext;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IDataContext dataContext,
        ICommunityRequestContext requestContext,
        ILogger<NotificationService> logger)
    {
        _dataContext = dataContext ?? throw new ArgumentNullException(nameof(dataContext));
        _requestContext = requestContext ?? throw new ArgumentNullException(nameof(requestContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<Result<CmdResponse>> MarkNotificationsReadAsync(
        MarkNotificationsReadRequest request,
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

            if (request.NotificationIds.Count == 0)
            {
                return Result<CmdResponse>.Failure("At least one notification ID is required", 400);
            }

            var notificationIds = request.NotificationIds.Distinct().ToList();

            var notifications = await _dataContext.Query<CommunityNotification>()
                .Where(n => n.TenantId == requester.TenantId)
                .Where(n => notificationIds.Contains(n.Id))
                .Where(n => n.RecipientIdentityId == requester.IdentityId)
                .Where(n => !n.IsDeleted)
                .ToListAsync(cancellationToken);

            if (notifications.Count != notificationIds.Count)
            {
                return Result<CmdResponse>.NotFound("One or more notifications were not found");
            }

            foreach (var notification in notifications)
            {
                notification.IsRead = true;
                notification.ModifiedAt = DateTime.UtcNow;
                _dataContext.Update(notification);
            }

            var saveResult = await _dataContext.SaveChangesAsync(cancellationToken);
            if (CommunitySecurity.SaveFailure(saveResult, "MarkNotificationsRead") is { } saveFailure)
                return saveFailure;

            _logger.CommunityNotificationsMarkedRead(notifications.Count);

            return Result<CmdResponse>.Success(new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.OK,
                Message = $"{notifications.Count} notification(s) marked as read"
            });
        }
        catch (Exception ex)
        {
            _logger.CommunityNotificationsMarkReadError(ex);
            return Result<CmdResponse>.Failure("An error occurred while marking notifications as read", 500);
        }
    }

    /// <inheritdoc />
    public async Task<Result<GetNotificationsResponse>> GetNotificationsAsync(
        GetNotificationsRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var requesterResult = await _requestContext.GetRequiredIdentityAsync(request.Metadata, cancellationToken);
            if (!requesterResult.IsSuccess)
                return CommunitySecurity.ToFailure<CommunityRequester, GetNotificationsResponse>(requesterResult);

            var requester = requesterResult.Data!;
            if (CommunitySecurity.IsSpoofed(request.IdentityId, requester.IdentityId)
                || CommunitySecurity.IsSpoofed(request.RequestingIdentityId, requester.IdentityId))
            {
                return Result<GetNotificationsResponse>.Forbidden("You can only retrieve your own notifications");
            }

            // Validate identity exists
            var identity = await _dataContext.Query<CommunityIdentity>()
                .Where(i => i.TenantId == requester.TenantId)
                .Where(i => i.Id == requester.IdentityId)
                .FirstOrDefaultAsync(cancellationToken);

            if (identity == null)
            {
                _logger.CommunityIdentityNotFound(requester.IdentityId);
                return Result<GetNotificationsResponse>.NotFound($"Identity with Id {requester.IdentityId} does not exist");
            }

            // Build query
            var query = _dataContext.Query<CommunityNotification>()
                .Where(n => n.TenantId == requester.TenantId)
                .Where(n => n.RecipientIdentityId == requester.IdentityId)
                .Where(n => !n.IsDeleted);

            // Apply optional IsRead filter
            if (request.IsRead.HasValue)
            {
                query = query.Where(n => n.IsRead == request.IsRead.Value);
            }

            var totalCount = await query.CountAsync(cancellationToken);

            var notificationEntities = await query
                .OrderByDescending(n => n.CreatedAt)
                .Skip(request.PageIndex * request.PageSize)
                .Take(request.PageSize)
                .Include(n => n.ActorIdentity)
                .ToListAsync(cancellationToken);

            // Project to response DTOs in memory
            var notifications = notificationEntities.Select(n => new NotificationItemResponse
            {
                Id = n.Id,
                RecipientIdentityId = n.RecipientIdentityId,
                ActorIdentityId = n.ActorIdentityId,
                ActorHandleName = n.ActorIdentity?.HandleName,
                Type = n.Type,
                Message = n.Message,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt
            }).ToList();

            _logger.CommunityNotificationsRetrieved(notifications.Count, requester.IdentityId);

            return Result<GetNotificationsResponse>.Success(new GetNotificationsResponse
            {
                Items = notifications,
                TotalCount = totalCount,
                PageIndex = request.PageIndex,
                PageSize = request.PageSize
            });
        }
        catch (Exception ex)
        {
            _logger.CommunityNotificationsError(request.IdentityId == Guid.Empty ? Guid.Empty : request.IdentityId, ex);
            return Result<GetNotificationsResponse>.Failure("An error occurred while retrieving notifications", 500);
        }
    }

    /// <inheritdoc />
    public async Task<Result<CmdResponse>> CreateNotificationAsync(
        Guid tenantId,
        Guid recipientIdentityId,
        Guid actorIdentityId,
        NotificationType type,
        Guid? referenceId,
        string message,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var notification = new CommunityNotification
            {
                TenantId = tenantId,
                RecipientIdentityId = recipientIdentityId,
                ActorIdentityId = actorIdentityId,
                Type = type.ToString(),
                Message = message,
                IsRead = false,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow
            };

            _dataContext.Add(notification);
            var saveResult = await _dataContext.SaveChangesAsync(cancellationToken);
            if (CommunitySecurity.SaveFailure(saveResult, "CreateNotification") is { } saveFailure)
                return saveFailure;

            _logger.CommunityNotificationCreated(notification.Id, recipientIdentityId);
            return Result<CmdResponse>.Success(new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.Created,
                Message = "Notification created successfully"
            }, 201);
        }
        catch (Exception ex)
        {
            _logger.CommunityNotificationCreateError(recipientIdentityId, ex);
            return Result<CmdResponse>.Failure("An error occurred while creating notification", 500);
        }
    }
}

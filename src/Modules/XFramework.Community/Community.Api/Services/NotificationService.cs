using Community.Domain.Shared.Contracts;
using Community.Domain.Shared.Contracts.Requests;
using Community.Domain.Shared.Contracts.Responses;
using Community.Domain.Shared.Enums;
using Microsoft.Extensions.Logging;
using XFramework.Core.Loggers;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.DataContext;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace Community.Api.Services;

/// <summary>
/// Service for managing community notifications.
/// </summary>
public sealed class NotificationService : INotificationService
{
    private readonly IDataContext _dataContext;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IDataContext dataContext,
        ILogger<NotificationService> logger)
    {
        _dataContext = dataContext ?? throw new ArgumentNullException(nameof(dataContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<Result<CmdResponse>> MarkNotificationsReadAsync(
        MarkNotificationsReadRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (request.NotificationIds.Count == 0)
            {
                return Result<CmdResponse>.Failure("At least one notification ID is required", 400);
            }

            var notifications = await _dataContext.Query<CommunityNotification>()
                .Where(n => request.NotificationIds.Contains(n.Id))
                .Where(n => !n.IsDeleted)
                .ToListAsync(cancellationToken);

            if (notifications.Count == 0)
            {
                return Result<CmdResponse>.NotFound("No notifications found for the provided IDs");
            }

            foreach (var notification in notifications)
            {
                notification.IsRead = true;
                notification.ModifiedAt = DateTime.UtcNow;
                _dataContext.Update(notification);
            }

            await _dataContext.SaveChangesAsync(cancellationToken);

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
            // Validate identity exists
            var identity = await _dataContext.Query<CommunityIdentity>()
                .Where(i => i.Id == request.IdentityId)
                .FirstOrDefaultAsync(cancellationToken);

            if (identity == null)
            {
                _logger.CommunityIdentityNotFound(request.IdentityId);
                return Result<GetNotificationsResponse>.NotFound($"Identity with Id {request.IdentityId} does not exist");
            }

            // Build query
            var query = _dataContext.Query<CommunityNotification>()
                .Where(n => n.RecipientIdentityId == request.IdentityId)
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
                ReferenceId = n.ReferenceId,
                Message = n.Message,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt
            }).ToList();

            _logger.CommunityNotificationsRetrieved(notifications.Count, request.IdentityId);

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
            _logger.CommunityNotificationsError(request.IdentityId, ex);
            return Result<GetNotificationsResponse>.Failure("An error occurred while retrieving notifications", 500);
        }
    }

    /// <inheritdoc />
    public async Task CreateNotificationAsync(
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
                RecipientIdentityId = recipientIdentityId,
                ActorIdentityId = actorIdentityId,
                Type = type,
                ReferenceId = referenceId,
                Message = message,
                IsRead = false,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow
            };

            _dataContext.Add(notification);
            await _dataContext.SaveChangesAsync(cancellationToken);

            _logger.CommunityNotificationCreated(notification.Id, recipientIdentityId);
        }
        catch (Exception ex)
        {
            // Log but don't fail the parent operation if notification creation fails
            _logger.CommunityNotificationCreateError(recipientIdentityId, ex);
        }
    }
}

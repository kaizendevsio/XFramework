namespace Community.Api.Services;

/// <summary>
/// Service for managing community notifications.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Marks the specified notifications as read.
    /// </summary>
    Task<Result<CmdResponse>> MarkNotificationsReadAsync(
        MarkNotificationsReadRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a paginated list of notifications for an identity.
    /// </summary>
    Task<Result<GetNotificationsResponse>> GetNotificationsAsync(
        GetNotificationsRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a notification (internal helper, not exposed as an endpoint).
    /// </summary>
    Task CreateNotificationAsync(
        Guid recipientIdentityId,
        Guid actorIdentityId,
        Community.Domain.Shared.Enums.NotificationType type,
        Guid? referenceId,
        string message,
        CancellationToken cancellationToken = default);
}

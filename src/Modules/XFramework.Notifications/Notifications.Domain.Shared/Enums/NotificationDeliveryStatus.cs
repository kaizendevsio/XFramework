namespace Notifications.Domain.Shared.Enums;

public enum NotificationDeliveryStatus
{
    Pending = 0,
    Queued = 1,
    Sent = 2,
    Delivered = 3,
    Failed = 4,
    Suppressed = 5,
    Cancelled = 6
}

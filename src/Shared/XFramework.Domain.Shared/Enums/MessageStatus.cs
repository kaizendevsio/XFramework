namespace XFramework.Domain.Shared.Enums;

public enum MessageStatus
{
    NotSpecified = 0,
    Queued = 1,
    Sent = 2,
    Delivered = 3,
    Blocked = 4,
    Scheduled = 5,
    Failed = 6,
    Processing = 7,
}
namespace SmsGateway.Domain.Generic.Enums;

public enum SmsStatus
{
    NotSpecified = 0,
    Queued = 1,
    Sent = 2,
    Delivered = 3,
    Blocked = 4,
    Scheduled = 5
}
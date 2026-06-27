namespace SmsGateway.Domain.Shared.Enums;

public enum SmsOutboundJobStatus
{
    Queued = 0,
    Leased = 1,
    Sending = 2,
    Sent = 3,
    Failed = 4,
    RetryScheduled = 5,
    DeadLettered = 6,
    Cancelled = 7
}

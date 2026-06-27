namespace Notifications.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
public partial class NotificationDeliveryJob : BaseModel
{
    [MemoryPackOrder(0)]
    public Guid NotificationInboxItemId { get; set; }

    [MemoryPackOrder(1)]
    public NotificationDeliveryChannel Channel { get; set; }

    [MemoryPackOrder(2)]
    public NotificationDeliveryStatus Status { get; set; } = NotificationDeliveryStatus.Queued;

    [MemoryPackOrder(3)]
    public string? ProviderKey { get; set; }

    [MemoryPackOrder(4)]
    public string? RecipientAddress { get; set; }

    [MemoryPackOrder(5)]
    public string? PayloadJson { get; set; }

    [MemoryPackOrder(6)]
    public string? CorrelationId { get; set; }

    [MemoryPackOrder(7)]
    public DateTime? NextAttemptAt { get; set; }

    [MemoryPackOrder(8)]
    public DateTime? LeasedUntil { get; set; }

    [MemoryPackOrder(9)]
    public string? LeaseOwner { get; set; }

    [MemoryPackOrder(10)]
    public int AttemptCount { get; set; }

    [MemoryPackOrder(11)]
    public int MaxAttempts { get; set; } = 5;

    [MemoryPackOrder(12)]
    public string? ProviderMessageId { get; set; }

    [MemoryPackOrder(13)]
    public string? LastErrorCode { get; set; }

    [MemoryPackOrder(14)]
    public string? LastErrorMessage { get; set; }

    [MemoryPackOrder(15)]
    public DateTime? CompletedAt { get; set; }

    [MemoryPackOrder(16)]
    public virtual NotificationInboxItem NotificationInboxItem { get; set; } = null!;

    [MemoryPackOrder(17)]
    public virtual ICollection<NotificationDeliveryAttempt> Attempts { get; set; } =
        new List<NotificationDeliveryAttempt>();
}

namespace Notifications.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
public partial class NotificationDeliveryAttempt : BaseModel
{
    [MemoryPackOrder(0)]
    public Guid NotificationDeliveryJobId { get; set; }

    [MemoryPackOrder(1)]
    public int AttemptNumber { get; set; }

    [MemoryPackOrder(2)]
    public NotificationDeliveryStatus Status { get; set; }

    [MemoryPackOrder(3)]
    public string? ProviderKey { get; set; }

    [MemoryPackOrder(4)]
    public string? ProviderMessageId { get; set; }

    [MemoryPackOrder(5)]
    public string? ErrorCode { get; set; }

    [MemoryPackOrder(6)]
    public string? ErrorMessage { get; set; }

    [MemoryPackOrder(7)]
    public DateTime StartedAt { get; set; }

    [MemoryPackOrder(8)]
    public DateTime? CompletedAt { get; set; }

    [MemoryPackOrder(9)]
    public virtual NotificationDeliveryJob NotificationDeliveryJob { get; set; } = null!;
}

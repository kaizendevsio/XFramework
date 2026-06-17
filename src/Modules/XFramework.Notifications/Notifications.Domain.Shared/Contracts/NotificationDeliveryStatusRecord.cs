namespace Notifications.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
public partial class NotificationDeliveryStatusRecord : BaseModel
{
    [MemoryPackOrder(0)]
    public Guid NotificationInboxItemId { get; set; }

    [MemoryPackOrder(1)]
    public NotificationDeliveryChannel Channel { get; set; }

    [MemoryPackOrder(2)]
    public NotificationDeliveryStatus Status { get; set; }

    [MemoryPackOrder(3)]
    public string? ProviderMessageId { get; set; }

    [MemoryPackOrder(4)]
    public string? ErrorCode { get; set; }

    [MemoryPackOrder(5)]
    public string? ErrorMessage { get; set; }

    [MemoryPackOrder(6)]
    public int AttemptNumber { get; set; }

    [MemoryPackOrder(7)]
    public DateTime RecordedAt { get; set; }

    [MemoryPackOrder(8)]
    public virtual NotificationInboxItem NotificationInboxItem { get; set; } = null!;
}

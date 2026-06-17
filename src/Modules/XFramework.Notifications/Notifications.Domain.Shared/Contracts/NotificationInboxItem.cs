namespace Notifications.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
public partial class NotificationInboxItem : BaseModel
{
    [MemoryPackOrder(0)]
    public Guid RecipientCredentialId { get; set; }

    [MemoryPackOrder(1)]
    public Guid? SourceCredentialId { get; set; }

    [MemoryPackOrder(2)]
    public string TemplateKey { get; set; } = NotificationTemplateKeys.SystemGeneric;

    [MemoryPackOrder(3)]
    public string Title { get; set; } = string.Empty;

    [MemoryPackOrder(4)]
    public string Body { get; set; } = string.Empty;

    [MemoryPackOrder(5)]
    public NotificationDeliveryChannel DeliveryChannels { get; set; } = NotificationPreferenceDefaults.EnabledChannels;

    [MemoryPackOrder(6)]
    public string? CorrelationId { get; set; }

    [MemoryPackOrder(7)]
    public string? DataJson { get; set; }

    [MemoryPackOrder(8)]
    public bool IsRead { get; set; }

    [MemoryPackOrder(9)]
    public DateTime? ReadAt { get; set; }

    [MemoryPackOrder(10)]
    public virtual ICollection<NotificationDeliveryStatusRecord> DeliveryStatuses { get; set; } =
        new List<NotificationDeliveryStatusRecord>();
}

namespace Notifications.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record NotificationDeliveryStatusResponse
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid NotificationInboxItemId { get; set; }
    public NotificationDeliveryChannel Channel { get; set; }
    public NotificationDeliveryStatus Status { get; set; }
    public string? ProviderMessageId { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public int AttemptNumber { get; set; }
    public DateTime RecordedAt { get; set; }
}

namespace Notifications.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record RecordNotificationDeliveryStatusRequest : RequestBase,
    ICommand<QueryResponse<NotificationDeliveryStatusResponse>>,
    IBoltRequest<RecordNotificationDeliveryStatusRequest, QueryResponse<NotificationDeliveryStatusResponse>>
{
    public Guid? TenantId { get; set; }
    public Guid NotificationInboxItemId { get; set; }
    public NotificationDeliveryChannel Channel { get; set; }
    public NotificationDeliveryStatus Status { get; set; }
    public string? ProviderMessageId { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public int AttemptNumber { get; set; }
    public DateTime? RecordedAt { get; set; }
}

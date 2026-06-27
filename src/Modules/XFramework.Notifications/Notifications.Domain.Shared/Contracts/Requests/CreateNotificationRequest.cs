namespace Notifications.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record CreateNotificationRequest : RequestBase,
    ICommand<QueryResponse<NotificationInboxItemResponse>>,
    IBoltRequest<CreateNotificationRequest, QueryResponse<NotificationInboxItemResponse>>
{
    public Guid? TenantId { get; set; }
    public Guid RecipientCredentialId { get; set; }
    public Guid? SourceCredentialId { get; set; }
    public string TemplateKey { get; set; } = Notifications.Domain.Shared.Contracts.NotificationTemplateKeys.SystemGeneric;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public NotificationDeliveryChannel DeliveryChannels { get; set; } =
        Notifications.Domain.Shared.Contracts.NotificationPreferenceDefaults.EnabledChannels;
    public string? CorrelationId { get; set; }
    public string? DeliveryAddress { get; set; }
    public Dictionary<string, string>? Data { get; set; }
}

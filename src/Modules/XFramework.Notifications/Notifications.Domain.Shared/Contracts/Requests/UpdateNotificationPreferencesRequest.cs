namespace Notifications.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record UpdateNotificationPreferencesRequest : RequestBase,
    ICommand<QueryResponse<NotificationPreferencesResponse>>,
    IBoltRequest<UpdateNotificationPreferencesRequest, QueryResponse<NotificationPreferencesResponse>>
{
    public Guid? TenantId { get; set; }
    public Guid CredentialId { get; set; }
    public NotificationDeliveryChannel EnabledChannels { get; set; } =
        Notifications.Domain.Shared.Contracts.NotificationPreferenceDefaults.EnabledChannels;
    public List<string> DisabledTemplateKeys { get; set; } = [];
    public bool DigestEnabled { get; set; }
}

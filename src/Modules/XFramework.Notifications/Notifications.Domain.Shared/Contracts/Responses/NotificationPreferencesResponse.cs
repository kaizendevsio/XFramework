namespace Notifications.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record NotificationPreferencesResponse
{
    public Guid? Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CredentialId { get; set; }
    public NotificationDeliveryChannel EnabledChannels { get; set; }
    public List<string> DisabledTemplateKeys { get; set; } = [];
    public bool DigestEnabled { get; set; }
    public bool IsDefault { get; set; }
}

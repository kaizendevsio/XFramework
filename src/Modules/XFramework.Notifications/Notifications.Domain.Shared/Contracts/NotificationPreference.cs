namespace Notifications.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
public partial class NotificationPreference : BaseModel
{
    [MemoryPackOrder(0)]
    public Guid CredentialId { get; set; }

    [MemoryPackOrder(1)]
    public NotificationDeliveryChannel EnabledChannels { get; set; } = NotificationPreferenceDefaults.EnabledChannels;

    [MemoryPackOrder(2)]
    public string? DisabledTemplateKeys { get; set; }

    [MemoryPackOrder(3)]
    public bool DigestEnabled { get; set; }
}

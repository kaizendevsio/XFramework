namespace Notifications.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record PushSubscriptionResponse
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CredentialId { get; set; }
    public Guid? DeviceId { get; set; }
    public DateTime LastSeenAt { get; set; }
}

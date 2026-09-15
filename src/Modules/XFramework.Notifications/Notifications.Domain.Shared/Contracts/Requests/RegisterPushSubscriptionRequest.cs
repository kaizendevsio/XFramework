namespace Notifications.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record RegisterPushSubscriptionRequest : RequestBase,
    ICommand<QueryResponse<PushSubscriptionResponse>>,
    IBoltRequest<RegisterPushSubscriptionRequest, QueryResponse<PushSubscriptionResponse>>
{
    public Guid? TenantId { get; set; }
    public Guid CredentialId { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public string P256dh { get; set; } = string.Empty;
    public string Auth { get; set; } = string.Empty;
    public Guid? DeviceId { get; set; }
    public string? DeviceLabel { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

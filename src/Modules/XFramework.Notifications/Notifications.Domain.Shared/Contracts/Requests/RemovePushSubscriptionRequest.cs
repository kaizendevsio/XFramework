namespace Notifications.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record RemovePushSubscriptionRequest : RequestBase,
    ICommand<CmdResponse>,
    IBoltRequest<RemovePushSubscriptionRequest, CmdResponse>
{
    public Guid? TenantId { get; set; }
    public Guid CredentialId { get; set; }
    public string Endpoint { get; set; } = string.Empty;
}

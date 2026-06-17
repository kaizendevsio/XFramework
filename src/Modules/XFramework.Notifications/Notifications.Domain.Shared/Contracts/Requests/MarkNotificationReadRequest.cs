namespace Notifications.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record MarkNotificationReadRequest : RequestBase,
    ICommand<CmdResponse>,
    IBoltRequest<MarkNotificationReadRequest, CmdResponse>
{
    public Guid? TenantId { get; set; }
    public Guid RecipientCredentialId { get; set; }
    public List<Guid> NotificationIds { get; set; } = [];
}

namespace Notifications.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record SetPushPresenceRequest : RequestBase,
    ICommand<CmdResponse>, IBoltRequest<SetPushPresenceRequest, CmdResponse>
{
    public Guid? TenantId { get; set; }
    public Guid CredentialId { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public Guid WindowId { get; set; }
    public bool Visible { get; set; }
}

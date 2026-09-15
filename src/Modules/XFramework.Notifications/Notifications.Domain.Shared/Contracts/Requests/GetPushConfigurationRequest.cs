namespace Notifications.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record GetPushConfigurationRequest : RequestBase,
    IQuery<QueryResponse<PushConfigurationResponse>>,
    IBoltRequest<GetPushConfigurationRequest, QueryResponse<PushConfigurationResponse>>
{
    public Guid? TenantId { get; set; }
    public Guid CredentialId { get; set; }
}

namespace IdentityServer.Domain.Shared.Contracts.Requests;

using TResponse = CmdResponse;

[MemoryPackable]
public partial record DeleteTenantRequest : RequestBase,
    ICommand<TResponse>,
    IBoltRequest<DeleteTenantRequest, TResponse>
{
    public Guid TenantId { get; set; }
}

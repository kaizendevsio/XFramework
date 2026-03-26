namespace Community.Domain.Shared.Contracts.Requests;

using TResponse = CmdResponse;

[MemoryPackable]
public partial record DeleteConnectionRequest : RequestBase,
    ICommand<TResponse>,
    IBoltRequest<DeleteConnectionRequest, TResponse>
{
    public Guid Id { get; set; }
    public Guid RequestingIdentityId { get; set; }
}

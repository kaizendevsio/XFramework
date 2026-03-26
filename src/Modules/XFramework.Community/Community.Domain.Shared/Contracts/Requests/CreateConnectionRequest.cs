namespace Community.Domain.Shared.Contracts.Requests;

using TResponse = CmdResponse;

[MemoryPackable]
public partial record CreateConnectionRequest : RequestBase,
    ICommand<TResponse>,
    IBoltRequest<CreateConnectionRequest, TResponse>
{
    public Guid SourceIdentityId { get; set; }
    public Guid TargetIdentityId { get; set; }
    public Guid TypeId { get; set; }
}

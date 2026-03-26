namespace Community.Domain.Shared.Contracts.Requests;

using TResponse = CmdResponse;

[MemoryPackable]
public partial record CreateContentReactionRequest : RequestBase,
    ICommand<TResponse>,
    IBoltRequest<CreateContentReactionRequest, TResponse>
{
    public Guid ContentId { get; set; }
    public Guid TypeId { get; set; }
    public Guid IdentityId { get; set; }
}

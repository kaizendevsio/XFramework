namespace Community.Domain.Shared.Contracts.Requests;

using TResponse = CmdResponse;

[MemoryPackable]
public partial record DeleteContentReactionRequest : RequestBase,
    ICommand<TResponse>,
    IBoltRequest<DeleteContentReactionRequest, TResponse>
{
    public Guid ContentId { get; set; }
    public Guid ReactionId { get; set; }
    public Guid RequesterId { get; set; }
}

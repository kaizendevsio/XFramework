namespace Messaging.Domain.Shared.Contracts.Requests.Reactions;

using TRequest = DeleteMessageReactionRequest;
using TResponse = CmdResponse;

[MemoryPackable]
public partial record DeleteMessageReactionRequest : RequestBase,
    ICommand<CmdResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid ReactionId { get; set; }
    public Guid RequesterCredentialId { get; set; }
}

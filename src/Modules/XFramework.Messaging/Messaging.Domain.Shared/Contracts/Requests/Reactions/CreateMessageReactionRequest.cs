namespace Messaging.Domain.Shared.Contracts.Requests.Reactions;

using TRequest = CreateMessageReactionRequest;
using TResponse = CmdResponse;

[MemoryPackable]
public partial record CreateMessageReactionRequest : RequestBase,
    ICommand<CmdResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid ThreadId { get; set; }
    public Guid MessageId { get; set; }
    public Guid TypeId { get; set; }
    public Guid RequesterCredentialId { get; set; }
}

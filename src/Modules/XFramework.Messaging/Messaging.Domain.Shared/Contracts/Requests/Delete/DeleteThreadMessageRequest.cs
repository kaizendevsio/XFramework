namespace Messaging.Domain.Shared.Contracts.Requests.Delete;

using TRequest = DeleteThreadMessageRequest;
using TResponse = CmdResponse;

[MemoryPackable]
public partial record DeleteThreadMessageRequest : RequestBase,
    ICommand<CmdResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid ThreadId { get; set; }
    public Guid MessageId { get; set; }
    public Guid RequesterCredentialId { get; set; }
}

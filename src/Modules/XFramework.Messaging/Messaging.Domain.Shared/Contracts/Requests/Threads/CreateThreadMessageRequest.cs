namespace Messaging.Domain.Shared.Contracts.Requests.Threads;

using TRequest = CreateThreadMessageRequest;
using TResponse = QueryResponse<CreateThreadMessageResponse>;

[MemoryPackable]
public partial record CreateThreadMessageRequest : RequestBase,
    ICommand<CmdResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid ThreadId { get; set; }
    public Guid SenderCredentialId { get; set; }
    public string Text { get; set; } = null!;
    public Guid? TypeId { get; set; }
}

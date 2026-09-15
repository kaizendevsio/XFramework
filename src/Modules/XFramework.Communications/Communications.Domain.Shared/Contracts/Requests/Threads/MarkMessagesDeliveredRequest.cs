namespace Communications.Domain.Shared.Contracts.Requests.Threads;

using TRequest = MarkMessagesDeliveredRequest;
using TResponse = CmdResponse;

[MemoryPackable]
public partial record MarkMessagesDeliveredRequest : RequestBase,
    ICommand<CmdResponse>, IBoltRequest<TRequest, TResponse>
{
    public Guid ThreadId { get; set; }
    public Guid RequesterCredentialId { get; set; }
    public List<Guid> MessageIds { get; set; } = [];
}

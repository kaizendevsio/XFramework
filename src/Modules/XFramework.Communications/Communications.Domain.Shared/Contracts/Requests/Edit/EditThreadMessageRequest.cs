namespace Communications.Domain.Shared.Contracts.Requests.Edit;

using TRequest = EditThreadMessageRequest;
using TResponse = CmdResponse;

[MemoryPackable]
public partial record EditThreadMessageRequest : RequestBase,
    ICommand<CmdResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid ThreadId { get; set; }
    public Guid MessageId { get; set; }
    public Guid RequesterCredentialId { get; set; }
    public string Text { get; set; } = null!;
}

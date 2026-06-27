namespace Communications.Domain.Shared.Contracts.Requests.Attachments;

using TRequest = DeleteMessageFileRequest;
using TResponse = CmdResponse;

[MemoryPackable]
public partial record DeleteMessageFileRequest : RequestBase,
    ICommand<CmdResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid ThreadId { get; set; }
    public Guid MessageId { get; set; }
    public Guid FileId { get; set; }
    public Guid RequesterCredentialId { get; set; }
}

namespace Communications.Domain.Shared.Contracts.Requests.Attachments;

using TRequest = CreateMessageFileRequest;
using TResponse = CmdResponse;

[MemoryPackable]
public partial record CreateMessageFileRequest : RequestBase,
    ICommand<CmdResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid ThreadId { get; set; }
    public Guid MessageId { get; set; }
    public Guid StorageFileId { get; set; }
    public Guid RequesterCredentialId { get; set; }
}

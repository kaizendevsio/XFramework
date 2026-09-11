using Storage.Domain.Shared.Contracts.Responses;

namespace Communications.Domain.Shared.Contracts.Requests.Attachments;

[MemoryPackable]
public partial record GetChatAttachmentDownloadUrlRequest : RequestBase,
    IQuery<QueryResponse<StorageDownloadUrlResponse>>,
    IBoltRequest<GetChatAttachmentDownloadUrlRequest, QueryResponse<StorageDownloadUrlResponse>>
{
    public Guid ThreadId { get; set; }
    public Guid MessageId { get; set; }
    public Guid FileId { get; set; }
    public int? ExpirationMinutes { get; set; }
}

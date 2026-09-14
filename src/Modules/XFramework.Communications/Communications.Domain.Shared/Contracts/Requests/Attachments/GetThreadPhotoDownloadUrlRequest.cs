using Storage.Domain.Shared.Contracts.Responses;

namespace Communications.Domain.Shared.Contracts.Requests.Attachments;

[MemoryPackable]
public partial record GetThreadPhotoDownloadUrlRequest : RequestBase,
    IQuery<QueryResponse<StorageDownloadUrlResponse>>,
    IBoltRequest<GetThreadPhotoDownloadUrlRequest, QueryResponse<StorageDownloadUrlResponse>>
{
    public Guid ThreadId { get; set; }
}

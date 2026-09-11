using Storage.Domain.Shared.Contracts.Responses;

namespace Storage.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record AbortChatStorageUploadSessionRequest : RequestBase,
    ICommand<CmdResponse>,
    IBoltRequest<AbortChatStorageUploadSessionRequest, CmdResponse>
{
    public Guid UploadSessionId { get; set; }
}

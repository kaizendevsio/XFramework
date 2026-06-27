namespace Storage.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record AbortStorageUploadSessionRequest : RequestBase,
    ICommand<CmdResponse>,
    IBoltRequest<AbortStorageUploadSessionRequest, CmdResponse>
{
    public Guid UploadSessionId { get; set; }
}

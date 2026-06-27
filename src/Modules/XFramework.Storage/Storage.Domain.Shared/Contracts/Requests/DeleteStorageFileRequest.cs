namespace Storage.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record DeleteStorageFileRequest : RequestBase,
    ICommand<CmdResponse>,
    IBoltRequest<DeleteStorageFileRequest, CmdResponse>
{
    public Guid StorageFileId { get; set; }
    public DateTime? RetentionUntil { get; set; }
}

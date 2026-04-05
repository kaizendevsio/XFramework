namespace Community.Domain.Shared.Contracts.Requests;

using TResponse = CmdResponse;

[MemoryPackable]
public partial record CreateContentFileVsaRequest : RequestBase,
    ICommand<TResponse>,
    IBoltRequest<CreateContentFileVsaRequest, TResponse>
{
    public Guid ContentId { get; set; }
    public Guid StorageFileId { get; set; }
    public Guid RequestingIdentityId { get; set; }
}

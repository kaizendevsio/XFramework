namespace Community.Domain.Shared.Contracts.Requests;

using TResponse = CmdResponse;

[MemoryPackable]
public partial record UpdateIdentityFileRequest : RequestBase,
    ICommand<TResponse>,
    IBoltRequest<UpdateIdentityFileRequest, TResponse>
{
    public Guid IdentityId { get; set; }
    public Guid FileId { get; set; }
    public Guid StorageFileId { get; set; }
    public Guid RequestingIdentityId { get; set; }
}

namespace Community.Domain.Shared.Contracts.Requests;

using TResponse = CmdResponse;

[MemoryPackable]
public partial record DeleteContentFileRequest : RequestBase,
    ICommand<TResponse>,
    IBoltRequest<DeleteContentFileRequest, TResponse>
{
    public Guid ContentId { get; set; }
    public Guid FileId { get; set; }
    public Guid RequestingIdentityId { get; set; }
}

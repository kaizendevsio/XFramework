namespace Community.Domain.Shared.Contracts.Requests;

using TResponse = CmdResponse;

[MemoryPackable]
public partial record CreateContentRequest : RequestBase,
    ICommand<TResponse>,
    IBoltRequest<CreateContentRequest, TResponse>
{
    public Guid IdentityId { get; set; }
    public string? Text { get; set; }
    public Guid TypeId { get; set; }
    public Guid? ParentContentId { get; set; }
}

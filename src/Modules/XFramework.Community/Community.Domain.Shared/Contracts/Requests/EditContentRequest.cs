namespace Community.Domain.Shared.Contracts.Requests;

using TResponse = CmdResponse;

[MemoryPackable]
public partial record EditContentRequest : RequestBase,
    ICommand<TResponse>,
    IBoltRequest<EditContentRequest, TResponse>
{
    public Guid ContentId { get; set; }
    public Guid RequestingIdentityId { get; set; }
    public string? Text { get; set; }
    public string? Title { get; set; }
}

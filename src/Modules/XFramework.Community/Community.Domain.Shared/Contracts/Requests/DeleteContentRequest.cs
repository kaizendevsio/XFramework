namespace Community.Domain.Shared.Contracts.Requests;

using TResponse = CmdResponse;

[MemoryPackable]
public partial record DeleteContentRequest : RequestBase,
    ICommand<TResponse>,
    IBoltRequest<DeleteContentRequest, TResponse>
{
    public Guid Id { get; set; }
    public Guid RequesterId { get; set; }
}

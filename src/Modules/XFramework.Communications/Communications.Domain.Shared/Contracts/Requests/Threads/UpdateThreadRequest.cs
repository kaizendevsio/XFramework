namespace Communications.Domain.Shared.Contracts.Requests.Threads;

using TRequest = UpdateThreadRequest;
using TResponse = CmdResponse;

[MemoryPackable]
public partial record UpdateThreadRequest : RequestBase,
    ICommand<CmdResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid ThreadId { get; set; }
    public Guid RequesterCredentialId { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public ConversationFeatures? Features { get; set; }
    public Guid? NicknameMemberId { get; set; }
    public string? Nickname { get; set; }
}

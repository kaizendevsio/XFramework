namespace Communications.Domain.Shared.Contracts.Requests.Threads;

using TRequest = CreateThreadMessageRequest;
using TResponse = QueryResponse<CreateThreadMessageResponse>;

[MemoryPackable]
public partial record CreateThreadMessageRequest : RequestBase,
    ICommand<CmdResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid ThreadId { get; set; }
    public Guid SenderCredentialId { get; set; }
    public string? Text { get; set; }
    public Guid? TypeId { get; set; }
    public Guid? ParentMessageId { get; set; }
    public List<Guid> MentionedCredentialIds { get; set; } = [];
    public Guid? TemplateId { get; set; }
    public string? TemplateKey { get; set; }
    public Dictionary<string, string> TemplateVariables { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

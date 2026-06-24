namespace Messaging.Domain.Shared.Contracts.Requests.Threads;

[MemoryPackable]
public partial record PinMessageRequest : RequestBase,
    ICommand<CmdResponse>,
    IBoltRequest<PinMessageRequest, CmdResponse>
{
    public Guid ThreadId { get; set; }
    public Guid MessageId { get; set; }
    public bool IsPinned { get; set; } = true;
}

[MemoryPackable]
public partial record UnpinMessageRequest : RequestBase,
    ICommand<CmdResponse>,
    IBoltRequest<UnpinMessageRequest, CmdResponse>
{
    public Guid ThreadId { get; set; }
    public Guid MessageId { get; set; }
}

[MemoryPackable]
public partial record SaveMessageRequest : RequestBase,
    ICommand<CmdResponse>,
    IBoltRequest<SaveMessageRequest, CmdResponse>
{
    public Guid ThreadId { get; set; }
    public Guid MessageId { get; set; }
    public bool IsSaved { get; set; } = true;
}

[MemoryPackable]
public partial record UnsaveMessageRequest : RequestBase,
    ICommand<CmdResponse>,
    IBoltRequest<UnsaveMessageRequest, CmdResponse>
{
    public Guid ThreadId { get; set; }
    public Guid MessageId { get; set; }
}

[MemoryPackable]
public partial record SearchMessagesRequest : RequestBase,
    IQuery<QueryResponse<SearchMessagesResponse>>,
    IBoltRequest<SearchMessagesRequest, QueryResponse<SearchMessagesResponse>>
{
    public string Query { get; set; } = string.Empty;
    public Guid? ThreadId { get; set; }
    public int PageIndex { get; set; }
    public int PageSize { get; set; } = 20;
}

[MemoryPackable]
public partial record ReportMessageRequest : RequestBase,
    ICommand<CmdResponse>,
    IBoltRequest<ReportMessageRequest, CmdResponse>
{
    public Guid ThreadId { get; set; }
    public Guid MessageId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Details { get; set; }
}

[MemoryPackable]
public partial record BlockCredentialRequest : RequestBase,
    ICommand<CmdResponse>,
    IBoltRequest<BlockCredentialRequest, CmdResponse>
{
    public Guid CredentialId { get; set; }
}

[MemoryPackable]
public partial record DeleteCredentialBlockRequest : RequestBase,
    ICommand<CmdResponse>,
    IBoltRequest<DeleteCredentialBlockRequest, CmdResponse>
{
    public Guid CredentialId { get; set; }
}

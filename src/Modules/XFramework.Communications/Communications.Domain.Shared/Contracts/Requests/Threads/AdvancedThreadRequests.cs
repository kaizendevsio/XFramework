namespace Communications.Domain.Shared.Contracts.Requests.Threads;

[MemoryPackable]
public partial record CreateDirectThreadRequest : RequestBase,
    ICommand<QueryResponse<CreateThreadResponse>>,
    IBoltRequest<CreateDirectThreadRequest, QueryResponse<CreateThreadResponse>>
{
    public Guid OtherCredentialId { get; set; }
    public Guid? TypeId { get; set; }
    public string? Name { get; set; }
}

[MemoryPackable]
public partial record GetUnreadCountsRequest : RequestBase,
    IQuery<QueryResponse<GetUnreadCountsResponse>>,
    IBoltRequest<GetUnreadCountsRequest, QueryResponse<GetUnreadCountsResponse>>;

[MemoryPackable]
public partial record LeaveThreadRequest : RequestBase,
    ICommand<CmdResponse>,
    IBoltRequest<LeaveThreadRequest, CmdResponse>
{
    public Guid ThreadId { get; set; }
}

[MemoryPackable]
public partial record MuteThreadRequest : RequestBase,
    ICommand<CmdResponse>,
    IBoltRequest<MuteThreadRequest, CmdResponse>
{
    public Guid ThreadId { get; set; }
    public bool IsMuted { get; set; }
}

[MemoryPackable]
public partial record ArchiveThreadRequest : RequestBase,
    ICommand<CmdResponse>,
    IBoltRequest<ArchiveThreadRequest, CmdResponse>
{
    public Guid ThreadId { get; set; }
    public bool IsArchived { get; set; }
}

[MemoryPackable]
public partial record CreateThreadInviteRequest : RequestBase,
    ICommand<CmdResponse>,
    IBoltRequest<CreateThreadInviteRequest, CmdResponse>
{
    public Guid ThreadId { get; set; }
    public Guid CredentialId { get; set; }
}

[MemoryPackable]
public partial record RespondThreadInviteRequest : RequestBase,
    ICommand<CmdResponse>,
    IBoltRequest<RespondThreadInviteRequest, CmdResponse>
{
    public Guid ThreadId { get; set; }
    public Guid InviteId { get; set; }
    public bool Accept { get; set; }
}

[MemoryPackable]
public partial record UpdateThreadMemberRoleRequest : RequestBase,
    ICommand<CmdResponse>,
    IBoltRequest<UpdateThreadMemberRoleRequest, CmdResponse>
{
    public Guid ThreadId { get; set; }
    public Guid MemberId { get; set; }
    public string Role { get; set; } = global::Communications.Domain.Shared.MessageThreadMemberRoles.Member;
}

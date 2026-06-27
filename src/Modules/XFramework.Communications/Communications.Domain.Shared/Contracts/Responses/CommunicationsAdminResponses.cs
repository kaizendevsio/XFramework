namespace Communications.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record CommunicationsAdminUsersSummary
{
    public int CommunicationsUserCount { get; set; }
    public int OnlineCount { get; set; }
    public int MutedUserCount { get; set; }
    public int BlockedUserCount { get; set; }
}

[MemoryPackable]
public partial record CommunicationsAdminThreadsSummary
{
    public int TotalThreads { get; set; }
    public int TotalMessages { get; set; }
    public int TotalMembers { get; set; }
    public int PendingOutboxCount { get; set; }
}

[MemoryPackable]
public partial record CommunicationsAdminUsersResponse
{
    public CommunicationsAdminUsersSummary Summary { get; set; } = new();
    public List<CommunicationsAdminUserRow> Items { get; set; } = [];
    public int TotalItemCount { get; set; }
}

[MemoryPackable]
public partial record CommunicationsAdminThreadsResponse
{
    public CommunicationsAdminThreadsSummary Summary { get; set; } = new();
    public List<CommunicationsAdminThreadRow> Items { get; set; } = [];
    public int TotalItemCount { get; set; }
}

[MemoryPackable]
public partial record CommunicationsAdminUserRow
{
    public Guid CredentialId { get; set; }
    public Guid? IdentityInfoId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string IdentityLabel { get; set; } = string.Empty;
    public bool IsOnline { get; set; }
    public DateTime? LastSeen { get; set; }
    public int ThreadCount { get; set; }
    public int MessageCount { get; set; }
    public int MutedThreadCount { get; set; }
    public int ArchivedThreadCount { get; set; }
    public int PendingInviteCount { get; set; }
    public int BlockRelationshipCount { get; set; }
    public string RoleSummary { get; set; } = string.Empty;
    [MemoryPackIgnore]
    public string PresenceText => IsOnline ? "Online" : "Offline";

    [MemoryPackIgnore]
    public string LastSeenText => LastSeen is null ? "Never seen" : $"Last seen {LastSeen:g}";
}

[MemoryPackable]
public partial record CommunicationsAdminThreadRow
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public int MemberCount { get; set; }
    public int MessageCount { get; set; }
    public int PendingInviteCount { get; set; }
    public int PinnedCount { get; set; }
    public int ReportCount { get; set; }
    public int MutedMemberCount { get; set; }
    public int ArchivedMemberCount { get; set; }
    public string LastMessagePreview { get; set; } = string.Empty;
    public DateTime? LastMessageAt { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
    [MemoryPackIgnore]
    public string StatusText => IsEnabled ? "Enabled" : "Disabled";

    [MemoryPackIgnore]
    public string MemberState => $"{MutedMemberCount} muted / {ArchivedMemberCount} archived";
}

[MemoryPackable]
public partial record CommunicationsAdminUserDetailResponse
{
    public CommunicationsAdminCredentialContext? Credential { get; set; }
    public CommunicationsAdminUserDetailSummary Summary { get; set; } = new();
    public List<CommunicationsAdminUserThreadRow> Threads { get; set; } = [];
    public List<CommunicationsAdminUserMessageRow> Messages { get; set; } = [];
    public List<CommunicationsAdminUserInviteRow> Invites { get; set; } = [];
    public List<CommunicationsAdminUserBlockRow> Blocks { get; set; } = [];
}

[MemoryPackable]
public partial record CommunicationsAdminCredentialContext
{
    public Guid Id { get; set; }
    public Guid? IdentityInfoId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string IdentityLabel { get; set; } = string.Empty;
    public string? UserAlias { get; set; }
    public bool IsOnline { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime? LastSeen { get; set; }
    public DateTime? OnlineSince { get; set; }
    public string? Device { get; set; }
    public string? LastActivityType { get; set; }
}

[MemoryPackable]
public partial record CommunicationsAdminUserDetailSummary
{
    public int ThreadCount { get; set; }
    public int MessageCount { get; set; }
    public int MutedThreadCount { get; set; }
    public int BlockRelationshipCount { get; set; }
}

[MemoryPackable]
public partial record CommunicationsAdminUserThreadRow
{
    public Guid ThreadId { get; set; }
    public string ThreadName { get; set; } = string.Empty;
    public string ThreadType { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsMuted { get; set; }
    public bool IsArchived { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public DateTime JoinedAt { get; set; }
}

[MemoryPackable]
public partial record CommunicationsAdminUserMessageRow
{
    public Guid MessageId { get; set; }
    public Guid ThreadId { get; set; }
    public string ThreadName { get; set; } = string.Empty;
    public string Preview { get; set; } = string.Empty;
    public bool HasParent { get; set; }
    public int MentionCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

[MemoryPackable]
public partial record CommunicationsAdminUserInviteRow
{
    public Guid InviteId { get; set; }
    public string ThreadName { get; set; } = string.Empty;
    public string StatusText { get; set; } = string.Empty;
    public string StatusKey { get; set; } = string.Empty;
    public string InvitedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

[MemoryPackable]
public partial record CommunicationsAdminUserBlockRow
{
    public Guid BlockId { get; set; }
    public string Direction { get; set; } = string.Empty;
    public string OtherCredential { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
}

[MemoryPackable]
public partial record CommunicationsAdminThreadDetailResponse
{
    public CommunicationsAdminThreadContext? Thread { get; set; }
    public CommunicationsAdminThreadDetailSummary Summary { get; set; } = new();
    public List<CommunicationsAdminThreadMemberRow> Members { get; set; } = [];
    public List<CommunicationsAdminThreadMessageRow> Messages { get; set; } = [];
}

[MemoryPackable]
public partial record CommunicationsAdminThreadContext
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public DateTime? LastMessageAt { get; set; }
}

[MemoryPackable]
public partial record CommunicationsAdminThreadDetailSummary
{
    public int MemberCount { get; set; }
    public int MessageCount { get; set; }
    public int MutedMemberCount { get; set; }
    public int ArchivedMemberCount { get; set; }
}

[MemoryPackable]
public partial record CommunicationsAdminThreadMemberRow
{
    public Guid MemberId { get; set; }
    public Guid CredentialId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Alias { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsMuted { get; set; }
    public bool IsArchived { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

[MemoryPackable]
public partial record CommunicationsAdminThreadMessageRow
{
    public Guid MessageId { get; set; }
    public Guid MemberId { get; set; }
    public Guid CredentialId { get; set; }
    public string Author { get; set; } = string.Empty;
    public string Preview { get; set; } = string.Empty;
    public bool IsReply { get; set; }
    public int MentionCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

[MemoryPackable]
public partial record CommunicationsAdminOperationsResponse
{
    public int PendingOutboxCount { get; set; }
    public int FailedOutboxCount { get; set; }
    public int PendingInviteCount { get; set; }
    public bool NotificationsEnabled { get; set; }
    public List<CommunicationsAdminOutboxRow> Outbox { get; set; } = [];
    public List<CommunicationsAdminOperationInviteRow> Invites { get; set; } = [];
    public List<CommunicationsAdminPinRow> Pins { get; set; } = [];
    public List<CommunicationsAdminSavedRow> SavedMessages { get; set; } = [];
}

[MemoryPackable]
public partial record CommunicationsAdminOutboxRow
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string AggregateType { get; set; } = string.Empty;
    public Guid? ThreadId { get; set; }
    public string Thread { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StatusKey { get; set; } = string.Empty;
    public int Attempts { get; set; }
    public DateTime OccurredAt { get; set; }
    public string ProcessedDisplay { get; set; } = string.Empty;
    public string LastError { get; set; } = string.Empty;
}

[MemoryPackable]
public partial record CommunicationsAdminOperationInviteRow
{
    public Guid Id { get; set; }
    public Guid ThreadId { get; set; }
    public string Thread { get; set; } = string.Empty;
    public string InvitedCredential { get; set; } = string.Empty;
    public string InvitedBy { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StatusKey { get; set; } = string.Empty;
    public string RespondedDisplay { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

[MemoryPackable]
public partial record CommunicationsAdminPinRow
{
    public Guid Id { get; set; }
    public Guid ThreadId { get; set; }
    public string Thread { get; set; } = string.Empty;
    public string MessagePreview { get; set; } = string.Empty;
    public string PinnedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

[MemoryPackable]
public partial record CommunicationsAdminSavedRow
{
    public Guid Id { get; set; }
    public Guid? ThreadId { get; set; }
    public string Thread { get; set; } = string.Empty;
    public string MessagePreview { get; set; } = string.Empty;
    public string SavedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

[MemoryPackable]
public partial record CommunicationsAdminModerationResponse
{
    public int OpenReportCount { get; set; }
    public int ReviewedReportCount { get; set; }
    public int DismissedReportCount { get; set; }
    public int ActiveBlockCount { get; set; }
    public List<CommunicationsAdminReportRow> Reports { get; set; } = [];
    public List<CommunicationsAdminBlockRow> Blocks { get; set; } = [];
    public List<CommunicationsAdminPolicyRow> Policies { get; set; } = [];
    public List<CommunicationsModerationRuleResponse> Rules { get; set; } = [];
}

[MemoryPackable]
public partial record CommunicationsAdminReportRow
{
    public Guid Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public string StatusKey { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public Guid? ThreadId { get; set; }
    public string Thread { get; set; } = string.Empty;
    public string MessagePreview { get; set; } = string.Empty;
    public string Reporter { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

[MemoryPackable]
public partial record CommunicationsAdminBlockRow
{
    public Guid Id { get; set; }
    public string Blocker { get; set; } = string.Empty;
    public string Blocked { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StatusKey { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

[MemoryPackable]
public partial record CommunicationsAdminPolicyRow
{
    public string Policy { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StatusKey { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
}

[MemoryPackable]
public partial record GetCommunicationsModerationRulesResponse
{
    public List<CommunicationsModerationRuleResponse> Items { get; set; } = [];
}

[MemoryPackable]
public partial record CommunicationsModerationRuleResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string MatchType { get; set; } = string.Empty;
    public string Pattern { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
}

[MemoryPackable]
public partial record CommunicationsReportWorkflowResponse
{
    public Guid ReportId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public List<CommunicationsReportAuditResponse> Audit { get; set; } = [];
}

[MemoryPackable]
public partial record CommunicationsReportAuditResponse
{
    public Guid Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public Guid? ActorCredentialId { get; set; }
    public Guid? AssignedCredentialId { get; set; }
    public short? FromStatus { get; set; }
    public short? ToStatus { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}

namespace Messaging.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record MessagingAdminUsersSummary
{
    public int MessagingUserCount { get; set; }
    public int OnlineCount { get; set; }
    public int MutedUserCount { get; set; }
    public int BlockedUserCount { get; set; }
}

[MemoryPackable]
public partial record MessagingAdminThreadsSummary
{
    public int TotalThreads { get; set; }
    public int TotalMessages { get; set; }
    public int TotalMembers { get; set; }
    public int PendingOutboxCount { get; set; }
}

[MemoryPackable]
public partial record MessagingAdminUsersResponse
{
    public MessagingAdminUsersSummary Summary { get; set; } = new();
    public List<MessagingAdminUserRow> Items { get; set; } = [];
    public int TotalItemCount { get; set; }
}

[MemoryPackable]
public partial record MessagingAdminThreadsResponse
{
    public MessagingAdminThreadsSummary Summary { get; set; } = new();
    public List<MessagingAdminThreadRow> Items { get; set; } = [];
    public int TotalItemCount { get; set; }
}

[MemoryPackable]
public partial record MessagingAdminUserRow
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
public partial record MessagingAdminThreadRow
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
public partial record MessagingAdminUserDetailResponse
{
    public MessagingAdminCredentialContext? Credential { get; set; }
    public MessagingAdminUserDetailSummary Summary { get; set; } = new();
    public List<MessagingAdminUserThreadRow> Threads { get; set; } = [];
    public List<MessagingAdminUserMessageRow> Messages { get; set; } = [];
    public List<MessagingAdminUserInviteRow> Invites { get; set; } = [];
    public List<MessagingAdminUserBlockRow> Blocks { get; set; } = [];
}

[MemoryPackable]
public partial record MessagingAdminCredentialContext
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
public partial record MessagingAdminUserDetailSummary
{
    public int ThreadCount { get; set; }
    public int MessageCount { get; set; }
    public int MutedThreadCount { get; set; }
    public int BlockRelationshipCount { get; set; }
}

[MemoryPackable]
public partial record MessagingAdminUserThreadRow
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
public partial record MessagingAdminUserMessageRow
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
public partial record MessagingAdminUserInviteRow
{
    public Guid InviteId { get; set; }
    public string ThreadName { get; set; } = string.Empty;
    public string StatusText { get; set; } = string.Empty;
    public string StatusKey { get; set; } = string.Empty;
    public string InvitedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

[MemoryPackable]
public partial record MessagingAdminUserBlockRow
{
    public Guid BlockId { get; set; }
    public string Direction { get; set; } = string.Empty;
    public string OtherCredential { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
}

[MemoryPackable]
public partial record MessagingAdminThreadDetailResponse
{
    public MessagingAdminThreadContext? Thread { get; set; }
    public MessagingAdminThreadDetailSummary Summary { get; set; } = new();
    public List<MessagingAdminThreadMemberRow> Members { get; set; } = [];
    public List<MessagingAdminThreadMessageRow> Messages { get; set; } = [];
}

[MemoryPackable]
public partial record MessagingAdminThreadContext
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
public partial record MessagingAdminThreadDetailSummary
{
    public int MemberCount { get; set; }
    public int MessageCount { get; set; }
    public int MutedMemberCount { get; set; }
    public int ArchivedMemberCount { get; set; }
}

[MemoryPackable]
public partial record MessagingAdminThreadMemberRow
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
public partial record MessagingAdminThreadMessageRow
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
public partial record MessagingAdminOperationsResponse
{
    public int PendingOutboxCount { get; set; }
    public int FailedOutboxCount { get; set; }
    public int PendingInviteCount { get; set; }
    public bool NotificationsEnabled { get; set; }
    public List<MessagingAdminOutboxRow> Outbox { get; set; } = [];
    public List<MessagingAdminOperationInviteRow> Invites { get; set; } = [];
    public List<MessagingAdminPinRow> Pins { get; set; } = [];
    public List<MessagingAdminSavedRow> SavedMessages { get; set; } = [];
}

[MemoryPackable]
public partial record MessagingAdminOutboxRow
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
public partial record MessagingAdminOperationInviteRow
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
public partial record MessagingAdminPinRow
{
    public Guid Id { get; set; }
    public Guid ThreadId { get; set; }
    public string Thread { get; set; } = string.Empty;
    public string MessagePreview { get; set; } = string.Empty;
    public string PinnedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

[MemoryPackable]
public partial record MessagingAdminSavedRow
{
    public Guid Id { get; set; }
    public Guid? ThreadId { get; set; }
    public string Thread { get; set; } = string.Empty;
    public string MessagePreview { get; set; } = string.Empty;
    public string SavedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

[MemoryPackable]
public partial record MessagingAdminModerationResponse
{
    public int OpenReportCount { get; set; }
    public int ReviewedReportCount { get; set; }
    public int DismissedReportCount { get; set; }
    public int ActiveBlockCount { get; set; }
    public List<MessagingAdminReportRow> Reports { get; set; } = [];
    public List<MessagingAdminBlockRow> Blocks { get; set; } = [];
    public List<MessagingAdminPolicyRow> Policies { get; set; } = [];
    public List<MessagingModerationRuleResponse> Rules { get; set; } = [];
}

[MemoryPackable]
public partial record MessagingAdminReportRow
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
public partial record MessagingAdminBlockRow
{
    public Guid Id { get; set; }
    public string Blocker { get; set; } = string.Empty;
    public string Blocked { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StatusKey { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

[MemoryPackable]
public partial record MessagingAdminPolicyRow
{
    public string Policy { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StatusKey { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
}

[MemoryPackable]
public partial record GetMessagingModerationRulesResponse
{
    public List<MessagingModerationRuleResponse> Items { get; set; } = [];
}

[MemoryPackable]
public partial record MessagingModerationRuleResponse
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
public partial record MessagingReportWorkflowResponse
{
    public Guid ReportId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public List<MessagingReportAuditResponse> Audit { get; set; } = [];
}

[MemoryPackable]
public partial record MessagingReportAuditResponse
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

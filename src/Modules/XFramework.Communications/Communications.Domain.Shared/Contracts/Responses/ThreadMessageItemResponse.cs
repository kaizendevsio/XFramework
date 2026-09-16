namespace Communications.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record ThreadMessageItemResponse
{
    public Guid Id { get; set; }
    public string Text { get; set; } = null!;
    public Guid SenderCredentialId { get; set; }
    public string SenderAlias { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public Guid? ParentMessageId { get; set; }
    public List<Guid> MentionedCredentialIds { get; set; } = [];
    public bool IsPinned { get; set; }
    public bool IsSaved { get; set; }
    public List<MessageReactionSummaryResponse> Reactions { get; set; } = [];
    public int ReplyCount { get; set; }
    public bool HasAttachments { get; set; }
    public bool IsThreadReply { get; set; }
    public int DeliveredCount { get; set; }
    public int ReadCount { get; set; }
    public List<Guid> ReadCredentialIds { get; set; } = [];
    public string? EncryptedEnvelope { get; set; }
    public long? AcceptedSenderDirectoryRevision { get; set; }
    public Guid? EncryptionSenderDeviceId { get; set; }
    public int PendingEncryptionCount { get; set; }
    public bool EncryptionPending { get; set; }
    public List<Guid> EncryptionAudienceCredentialIds { get; set; } = [];
    public bool IsLatestOwnMessage { get; set; }
    public List<Guid> LatestReadCredentialIds { get; set; } = [];
    public bool IsCallSummary { get; set; }
}

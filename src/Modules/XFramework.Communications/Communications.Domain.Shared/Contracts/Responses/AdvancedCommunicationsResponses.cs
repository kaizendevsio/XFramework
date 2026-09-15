namespace Communications.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record UnreadThreadCountResponse
{
    public Guid ThreadId { get; set; }
    public int UnreadCount { get; set; }
}

[MemoryPackable]
public partial record GetUnreadCountsResponse
{
    public List<UnreadThreadCountResponse> Threads { get; set; } = [];
    public int TotalUnreadCount { get; set; }
}

[MemoryPackable]
public partial record SearchMessagesResponse
{
    public List<SearchMessageItemResponse> Items { get; set; } = [];
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}

[MemoryPackable]
public partial record SearchMessageItemResponse
{
    public Guid ThreadId { get; set; }
    public Guid MessageId { get; set; }
    public Guid SenderCredentialId { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

[MemoryPackable]
public partial record GetSavedMessagesResponse
{
    public List<SavedMessageItemResponse> Items { get; set; } = [];
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}

/// <summary>
/// A message the requester saved, plus the conversation context the requester needs to
/// name and reopen it. Ciphertext travels untouched; only the recipient device can read it.
/// </summary>
[MemoryPackable]
public partial record SavedMessageItemResponse
{
    public Guid ThreadId { get; set; }
    public string ThreadName { get; set; } = string.Empty;
    public bool IsDirect { get; set; }
    public bool HasCustomName { get; set; }
    public Guid? ThreadPhotoStorageFileId { get; set; }
    /// <summary>The other side of a direct thread, so the caller can be named by the directory.</summary>
    public Guid? OtherCredentialId { get; set; }
    public Guid MessageId { get; set; }
    public Guid SenderCredentialId { get; set; }
    public string SenderAlias { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string? EncryptedEnvelope { get; set; }
    public long? AcceptedSenderDirectoryRevision { get; set; }
    public Guid? EncryptionSenderDeviceId { get; set; }
    public bool EncryptionPending { get; set; }
    public Guid? ParentMessageId { get; set; }
    public bool IsThreadReply { get; set; }
    public bool HasAttachments { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime SavedAt { get; set; }
}

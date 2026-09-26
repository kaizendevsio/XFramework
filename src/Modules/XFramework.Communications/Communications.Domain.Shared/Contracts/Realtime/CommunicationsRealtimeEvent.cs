namespace Communications.Domain.Shared.Contracts.Realtime;

[MemoryPackable]
public partial record CommunicationsRealtimeEvent
{
    public Guid EventId { get; set; }
    public Guid TenantId { get; set; }
    public Guid? ThreadId { get; set; }
    public Guid? ActorCredentialId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public long Sequence { get; set; }
    public string PayloadJson { get; set; } = "{}";
}

[MemoryPackable]
public partial record CommunicationsTypingState
{
    public Guid TenantId { get; set; }
    public Guid ThreadId { get; set; }
    public Guid CredentialId { get; set; }
    public bool IsTyping { get; set; }
    public DateTime OccurredAt { get; set; }
    // Appended after the original members so older MemoryPack readers and writers stay compatible:
    // a missing value reads as plain typing.
    public CommunicationsTypingActivity Activity { get; set; }
    /// <summary>How many items an attachment activity covers (never names, sizes or content); 0 for typing.</summary>
    public int Count { get; set; }
}

/// <summary>What a member is composing. Everything other than <see cref="Typing"/> is an attachment
/// in progress; it rides the typing channel, so the conversation's typing setting governs it too.</summary>
public enum CommunicationsTypingActivity
{
    Typing = 0,
    Photo = 1,
    Video = 2,
    File = 3,
    VoiceMessage = 4,
    Recording = 5
}

[MemoryPackable]
public partial record CommunicationsPresenceState
{
    public Guid TenantId { get; set; }
    public Guid CredentialId { get; set; }
    public bool IsOnline { get; set; }
    public DateTime LastActiveAt { get; set; }
}

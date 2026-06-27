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
}

[MemoryPackable]
public partial record CommunicationsPresenceState
{
    public Guid TenantId { get; set; }
    public Guid CredentialId { get; set; }
    public bool IsOnline { get; set; }
    public DateTime LastActiveAt { get; set; }
}

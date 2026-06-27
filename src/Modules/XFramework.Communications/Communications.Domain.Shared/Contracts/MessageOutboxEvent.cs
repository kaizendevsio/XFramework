namespace Communications.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
public partial class MessageOutboxEvent : BaseModel
{
    [MemoryPackOrder(0)]
    public string EventType { get; set; } = null!;

    [MemoryPackOrder(1)]
    public string AggregateType { get; set; } = null!;

    [MemoryPackOrder(2)]
    public Guid AggregateId { get; set; }

    [MemoryPackOrder(3)]
    public Guid? ThreadId { get; set; }

    [MemoryPackOrder(4)]
    public Guid? ActorCredentialId { get; set; }

    [MemoryPackOrder(5)]
    public string PayloadJson { get; set; } = "{}";

    [MemoryPackOrder(6)]
    public DateTime OccurredAt { get; set; }

    [MemoryPackOrder(7)]
    public DateTime? ProcessedAt { get; set; }

    [MemoryPackOrder(8)]
    public int Attempts { get; set; }

    [MemoryPackOrder(9)]
    public string? LastError { get; set; }

    [MemoryPackOrder(10)]
    public DateTime? NextAttemptAt { get; set; }

    [MemoryPackOrder(11)]
    public DateTime? LastAttemptAt { get; set; }

    [MemoryPackOrder(12)]
    public DateTime? DeadLetteredAt { get; set; }

    [MemoryPackOrder(13)]
    public string? LeaseOwner { get; set; }

    [MemoryPackOrder(14)]
    public DateTime? LeaseExpiresAt { get; set; }

    [MemoryPackOrder(15)]
    public DateTime? RealtimeProcessedAt { get; set; }

    [MemoryPackOrder(16)]
    public DateTime? NotificationProcessedAt { get; set; }

    [MemoryPackOrder(17)]
    public int RealtimeAttempts { get; set; }

    [MemoryPackOrder(18)]
    public int NotificationAttempts { get; set; }
}

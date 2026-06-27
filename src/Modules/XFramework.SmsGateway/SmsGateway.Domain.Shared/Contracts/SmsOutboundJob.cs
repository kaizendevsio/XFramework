using SmsGateway.Domain.Shared.Enums;
using XFramework.Domain.Shared.Contracts.Base;

namespace SmsGateway.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
public partial class SmsOutboundJob : BaseModel
{
    [MemoryPackOrder(0)]
    public Guid AgentClusterId { get; set; }

    [MemoryPackOrder(1)]
    public string? Sender { get; set; }

    [MemoryPackOrder(2)]
    public string Recipient { get; set; } = string.Empty;

    [MemoryPackOrder(3)]
    public string? Subject { get; set; }

    [MemoryPackOrder(4)]
    public string? Intent { get; set; }

    [MemoryPackOrder(5)]
    public string Message { get; set; } = string.Empty;

    [MemoryPackOrder(6)]
    public SmsOutboundJobStatus Status { get; set; } = SmsOutboundJobStatus.Queued;

    [MemoryPackOrder(7)]
    public DateTime? ScheduledAt { get; set; }

    [MemoryPackOrder(8)]
    public DateTime? NextAttemptAt { get; set; }

    [MemoryPackOrder(9)]
    public DateTime? LeasedUntil { get; set; }

    [MemoryPackOrder(10)]
    public string? LeaseOwner { get; set; }

    [MemoryPackOrder(11)]
    public int AttemptCount { get; set; }

    [MemoryPackOrder(12)]
    public int MaxAttempts { get; set; } = 5;

    [MemoryPackOrder(13)]
    public string? CorrelationId { get; set; }

    [MemoryPackOrder(14)]
    public Guid? NotificationDeliveryJobId { get; set; }

    [MemoryPackOrder(15)]
    public string? ProviderMessageId { get; set; }

    [MemoryPackOrder(16)]
    public string? LastErrorCode { get; set; }

    [MemoryPackOrder(17)]
    public string? LastErrorMessage { get; set; }

    [MemoryPackOrder(18)]
    public DateTime? SentAt { get; set; }

    [MemoryPackOrder(19)]
    public DateTime? DeadLetteredAt { get; set; }

    [MemoryPackOrder(20)]
    public virtual ICollection<SmsDeliveryAttempt> Attempts { get; set; } = new List<SmsDeliveryAttempt>();
}

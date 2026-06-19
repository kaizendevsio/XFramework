using XFramework.Domain.Shared.Attributes;

namespace Wallets.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.Get | EndpointActions.GetList,
    RoutePrefix = "api/wallet-outbox-messages",
    RequireAuthorization = true,
    CacheDurationSeconds = 30,
    CacheKeyPrefix = "wallet-outbox-messages"
)]
public partial class WalletOutboxMessage : BaseModel
{
    [MemoryPackOrder(0)]
    public Guid? OperationId { get; set; }

    [MemoryPackOrder(1)]
    public string EventType { get; set; } = string.Empty;

    [MemoryPackOrder(2)]
    public string AggregateType { get; set; } = string.Empty;

    [MemoryPackOrder(3)]
    public Guid AggregateId { get; set; }

    [MemoryPackOrder(4)]
    public string PayloadJson { get; set; } = "{}";

    [MemoryPackOrder(5)]
    public string? HeadersJson { get; set; }

    [MemoryPackOrder(6)]
    public WalletOutboxStatus Status { get; set; } = WalletOutboxStatus.Pending;

    [MemoryPackOrder(7)]
    public int Attempts { get; set; }

    [MemoryPackOrder(8)]
    public DateTime? NextAttemptAt { get; set; }

    [MemoryPackOrder(9)]
    public DateTime? PublishedAt { get; set; }

    [MemoryPackOrder(10)]
    public string? LastError { get; set; }

    [MemoryPackOrder(11)]
    public DateTime? LockedUntil { get; set; }

    [MemoryPackOrder(12)]
    public string? LockedBy { get; set; }

    [MemoryPackOrder(13)]
    public DateTime? LastAttemptAt { get; set; }

    [MemoryPackOrder(14)]
    public DateTime? DeadLetteredAt { get; set; }

    [MemoryPackOrder(15)]
    public int MaxAttempts { get; set; } = 5;

    [MemoryPackOrder(16)]
    public virtual WalletOperation? Operation { get; set; }
}

public class GetWalletOutboxMessageListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public Guid? OperationId { get; set; }
    public string? EventType { get; set; }
    public WalletOutboxStatus? Status { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

using XFramework.Domain.Shared.Attributes;

namespace Wallets.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.Get | EndpointActions.GetList,
    RoutePrefix = "api/wallet-payment-webhook-events",
    RequireAuthorization = true,
    AuthorizationFeature = "wallets.webhooks",
    ReadCapability = "manage",
    CacheDurationSeconds = 30,
    CacheKeyPrefix = "wallet-payment-webhook-events"
)]
public partial class WalletPaymentWebhookEvent : BaseModel
{
    [MemoryPackOrder(0)]
    public string ProviderKey { get; set; } = string.Empty;

    [MemoryPackOrder(1)]
    public string ExternalEventId { get; set; } = string.Empty;

    [MemoryPackOrder(2)]
    public string? ExternalReference { get; set; }

    [MemoryPackOrder(3)]
    public string? ProviderTransactionId { get; set; }

    [MemoryPackOrder(4)]
    public string? ProviderStatus { get; set; }

    [MemoryPackOrder(5)]
    public WalletWorkflowStatus? MappedWorkflowStatus { get; set; }

    [MemoryPackOrder(6)]
    public WalletWebhookProcessingStatus ProcessingStatus { get; set; } = WalletWebhookProcessingStatus.Received;

    [MemoryPackOrder(7)]
    public bool SignatureValid { get; set; }

    [MemoryPackOrder(8)]
    public string? SignatureScheme { get; set; }

    [MemoryPackOrder(9)]
    public string? HeadersHash { get; set; }

    [MemoryPackOrder(10)]
    public string RawPayloadJson { get; set; } = "{}";

    [MemoryPackOrder(11)]
    public string? ProcessingError { get; set; }

    [MemoryPackOrder(12)]
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    [MemoryPackOrder(13)]
    public DateTime? ProcessedAt { get; set; }

    [MemoryPackOrder(14)]
    public Guid? DepositRequestId { get; set; }

    [MemoryPackOrder(15)]
    public Guid? WithdrawalRequestId { get; set; }

    [MemoryPackOrder(16)]
    public Guid? OperationId { get; set; }

    [MemoryPackOrder(17)]
    public virtual DepositRequest? DepositRequest { get; set; }

    [MemoryPackOrder(18)]
    public virtual WithdrawalRequest? WithdrawalRequest { get; set; }

    [MemoryPackOrder(19)]
    public virtual WalletOperation? Operation { get; set; }
}

public class GetWalletPaymentWebhookEventListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? ProviderKey { get; set; }
    public string? ExternalEventId { get; set; }
    public string? ExternalReference { get; set; }
    public WalletWebhookProcessingStatus? ProcessingStatus { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

using XFramework.Domain.Shared.Attributes;

namespace Wallets.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.Get | EndpointActions.GetList,
    RoutePrefix = "api/wallet-policy-rules",
    RequireAuthorization = true,
    AuthorizationFeature = "wallets.policy",
    ReadCapability = "manage",
    CacheDurationSeconds = 60,
    CacheKeyPrefix = "wallet-policy-rules"
)]
public partial class WalletPolicyRule : BaseModel
{
    [MemoryPackOrder(0)]
    public string Name { get; set; } = string.Empty;

    [MemoryPackOrder(1)]
    public WalletOperationType? OperationType { get; set; }

    [MemoryPackOrder(2)]
    public Guid? WalletTypeId { get; set; }

    [MemoryPackOrder(3)]
    public Guid? CurrencyId { get; set; }

    [MemoryPackOrder(4)]
    public WalletStatus? RequiredWalletStatus { get; set; }

    [MemoryPackOrder(5)]
    public decimal? MaxSingleTransactionAmount { get; set; }

    [MemoryPackOrder(6)]
    public decimal? DailyVelocityLimit { get; set; }

    [MemoryPackOrder(7)]
    public decimal? MonthlyVelocityLimit { get; set; }

    [MemoryPackOrder(8)]
    public decimal? ApprovalThreshold { get; set; }

    [MemoryPackOrder(9)]
    public bool DenyWhenMatched { get; set; }

    [MemoryPackOrder(10)]
    public string? RiskTier { get; set; }

    [MemoryPackOrder(11)]
    public string? DecisionCode { get; set; }

    [MemoryPackOrder(12)]
    public DateTime EffectiveAt { get; set; } = DateTime.UtcNow;

    [MemoryPackOrder(13)]
    public DateTime? ExpiresAt { get; set; }

    [MemoryPackOrder(14)]
    public virtual WalletType? WalletType { get; set; }

    [MemoryPackOrder(15)]
    public virtual CurrencyType? Currency { get; set; }
}

public class GetWalletPolicyRuleListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public bool? IsEnabled { get; set; }
    public WalletOperationType? OperationType { get; set; }
    public Guid? WalletTypeId { get; set; }
    public Guid? CurrencyId { get; set; }
}

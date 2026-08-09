using XFramework.Domain.Shared.Attributes;

namespace Wallets.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.Get | EndpointActions.GetList,
    RoutePrefix = "api/wallet-fee-schedules",
    RequireAuthorization = true,
    AuthorizationFeature = "wallets.policy",
    ReadCapability = "manage",
    CacheDurationSeconds = 60,
    CacheKeyPrefix = "wallet-fee-schedules"
)]
public partial class WalletFeeSchedule : BaseModel
{
    [MemoryPackOrder(0)]
    public string Name { get; set; } = string.Empty;

    [MemoryPackOrder(1)]
    public WalletOperationType OperationType { get; set; }

    [MemoryPackOrder(2)]
    public Guid? WalletTypeId { get; set; }

    [MemoryPackOrder(3)]
    public Guid? CurrencyId { get; set; }

    [MemoryPackOrder(4)]
    public decimal FixedFee { get; set; }

    [MemoryPackOrder(5)]
    public decimal PercentageFee { get; set; }

    [MemoryPackOrder(6)]
    public decimal? MinimumFee { get; set; }

    [MemoryPackOrder(7)]
    public decimal? MaximumFee { get; set; }

    [MemoryPackOrder(8)]
    public bool AllowRequestedFeeOverride { get; set; }

    [MemoryPackOrder(9)]
    public DateTime EffectiveAt { get; set; } = DateTime.UtcNow;

    [MemoryPackOrder(10)]
    public DateTime? ExpiresAt { get; set; }

    [MemoryPackOrder(11)]
    public virtual WalletType? WalletType { get; set; }

    [MemoryPackOrder(12)]
    public virtual CurrencyType? Currency { get; set; }
}

public class GetWalletFeeScheduleListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public bool? IsEnabled { get; set; }
    public WalletOperationType? OperationType { get; set; }
    public Guid? WalletTypeId { get; set; }
    public Guid? CurrencyId { get; set; }
}

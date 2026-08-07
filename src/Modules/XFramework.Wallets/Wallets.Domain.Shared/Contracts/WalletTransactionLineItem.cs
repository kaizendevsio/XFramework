using System.ComponentModel.DataAnnotations.Schema;
using XFramework.Domain.Shared.Attributes;

namespace Wallets.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.Get | EndpointActions.GetList,
    RoutePrefix = "api/wallet-transaction-line-items",
    RequireAuthorization = true,
    AuthorizationFeature = "wallets.reporting",
    CacheDurationSeconds = 600,
    CacheKeyPrefix = "wallet-transaction-line-items"
)]
public partial class WalletTransactionLineItem : BaseModel
{
    [MemoryPackOrder(0)]
    public decimal? Amount { get; set; }
        
    [MemoryPackOrder(1)]
    public decimal Fee { get; set; }
        
    [MemoryPackOrder(2)]
    public string? Description { get; set; }

    [MemoryPackOrder(3)]
    public Guid WalletTransferId { get; set; }

    [ForeignKey(nameof(WalletTransferId))]
    [MemoryPackOrder(4)]
    public virtual WalletTransfer WalletTransfer { get; set; } = null!;
}

public class CreateWalletTransactionLineItemRequest
{
    public decimal? Amount { get; set; }
    public decimal Fee { get; set; }
    public string? Description { get; set; }
    public Guid WalletTransferId { get; set; }
}

public class UpdateWalletTransactionLineItemRequest
{
    public decimal? Amount { get; set; }
    public decimal Fee { get; set; }
    public string? Description { get; set; }
}

public class GetWalletTransactionLineItemListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
    public Guid? WalletTransferId { get; set; }
}

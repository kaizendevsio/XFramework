using System.ComponentModel.DataAnnotations.Schema;
using XFramework.Domain.Shared.Attributes;
using XFramework.Domain.Shared.Enums;

namespace Wallets.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.Get | EndpointActions.GetList,
    RoutePrefix = "api/wallet-transfers",
    RequireAuthorization = true,
    CacheDurationSeconds = 300,
    CacheKeyPrefix = "wallet-transfers"
)]
public partial class WalletTransfer : BaseModel
{
    [MemoryPackOrder(0)]
    public TransactionPurpose TransactionPurpose { get; set; } = XFramework.Domain.Shared.Enums.TransactionPurpose.Transfer;
        
    [MemoryPackOrder(1)]
    public Guid SenderTransactionId { get; set; }

    [MemoryPackOrder(2)]
    public Guid RecipientTransactionId { get; set; }

    [MemoryPackIgnore]
    [NotMapped]
    public decimal Amount => LineItems.Sum(x => x .Amount ?? 0);

    [MemoryPackOrder(3)]
    public decimal TransactionFee { get; set; }
    
    [MemoryPackOrder(4)]
    [NotMapped]
    public decimal TotalFees => LineItems.Sum(x => x.Fee) + TransactionFee;

    [MemoryPackOrder(5)]
    public virtual WalletTransaction SenderTransaction { get; set; } = null!;

    [MemoryPackOrder(6)]
    public virtual WalletTransaction RecipientTransaction { get; set; } = null!;

    [MemoryPackOrder(7)]
    public virtual ICollection<WalletTransactionLineItem> LineItems { get; set; } = new List<WalletTransactionLineItem>();
}

public class GetWalletTransferListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public Guid? SenderTransactionId { get; set; }
    public Guid? RecipientTransactionId { get; set; }
    public TransactionPurpose? TransactionPurpose { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}
using System.ComponentModel.DataAnnotations.Schema;
using XFramework.Domain.Shared.Attributes;

namespace Wallets.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.Get | EndpointActions.GetList,
    RoutePrefix = "api/wallet-transactions",
    RequireAuthorization = true,
    CacheDurationSeconds = 300,
    CacheKeyPrefix = "wallet-transactions"
)]
public partial class WalletTransaction : BaseModel
{
    [MemoryPackOrder(0)]
    public Guid CredentialId { get; set; }

    [MemoryPackOrder(1)]
    public Guid WalletId { get; set; }

    [MemoryPackOrder(2)]
    public decimal Amount
    {
        get
        {
            if (TransactionType is null)
            {
                return 0;
            }

            return decimal.Abs(InternalAmount) * (TransactionType switch
            {
                XFramework.Domain.Shared.Enums.TransactionType.Credit => 1,
                XFramework.Domain.Shared.Enums.TransactionType.Debit => -1,
                _ => 0
            });
        }
        set => InternalAmount = value;
    }

    [NotMapped]
    [MemoryPackOrder(3)]
    private decimal InternalAmount { get; set; }

    [MemoryPackOrder(4)]
    public decimal NetAmount
    {
        get => (Amount + LineItems.Sum(x => x.Amount) ?? 0) - TotalFees;
        set => _ = value;
    }

    [MemoryPackOrder(5)]
    public bool Held { get; set; }
    
    [MemoryPackOrder(6)]
    public bool Released { get; set; }
    
    [MemoryPackOrder(7)]
    public string? Remarks { get; set; }

    [MemoryPackOrder(8)]
    public decimal TransactionFee
    {
        get => _transactionFee;
        set => _transactionFee = value;
    }
    private decimal _transactionFee;

    [MemoryPackOrder(9)]
    public decimal TotalFees { 
        get => TransactionFee + LineItems.Sum(x => x.Fee); 
        set => _ = value; 
    }

    [MemoryPackOrder(10)]
    public string? ReferenceNumber { get; set; }
    
    [MemoryPackOrder(11)]
    public string? Description { get; set; }

    [MemoryPackOrder(12)]
    public decimal? RunningTotalBalance { get; set; }

    [MemoryPackOrder(13)]
    public decimal? RunningAvailableBalance { get; set; }
   
    [MemoryPackOrder(14)]
    public decimal? RunningBalance { get; set; }
    
    [MemoryPackOrder(15)]
    public decimal? RunningDebitOnHoldBalance { get; set; }
    
    [MemoryPackOrder(16)]
    public decimal? RunningCreditOnHoldBalance { get; set; }
    
    [MemoryPackOrder(17)]
    public decimal PreviousTotalBalance { get; set; }
    
    [MemoryPackOrder(18)]
    public decimal PreviousBalance { get; set; }
    
    [MemoryPackOrder(19)]
    public decimal PreviousDebitOnHoldBalance { get; set; }
    
    [MemoryPackOrder(20)]
    public decimal PreviousCreditOnHoldBalance { get; set; }

    [MemoryPackOrder(21)]
    public TransactionType? TransactionType { get; set; }

    [MemoryPackOrder(22)]
    public virtual IdentityCredential Credential { get; set; } = null!;

    [MemoryPackOrder(23)]
    public virtual Wallet? Wallet { get; set; }

    [MemoryPackOrder(24)]
    public virtual ICollection<WalletTransactionLineItem> LineItems { get; set; } = [];

}

public class GetWalletTransactionListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
    public Guid? CredentialId { get; set; }
    public Guid? WalletId { get; set; }
    public TransactionType? TransactionType { get; set; }
    public bool? Held { get; set; }
    public bool? Released { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}
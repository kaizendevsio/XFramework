using System.ComponentModel.DataAnnotations.Schema;
using XFramework.Domain.Shared.Enums;

namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class WalletTransaction : BaseModel
{
    
    [MemoryPackOrder(0)]
    public Guid CredentialId { get; set; }

    [MemoryPackOrder(1)]
    public Guid WalletId { get; set; }

    [MemoryPackIgnore]
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
                Enums.TransactionType.Credit => 1,
                Enums.TransactionType.Debit => -1,
                _ => 0
            });
        }
        set => InternalAmount = value;
    }

    [NotMapped]
    private decimal InternalAmount { get; set; }

    [MemoryPackIgnore]
    public decimal NetAmount => Amount - TransactionFee - ConvenienceFee;
    
    [MemoryPackOrder(2)]
    public bool Held { get; set; }
    
    [MemoryPackOrder(3)]
    public bool Released { get; set; }
    
    [MemoryPackOrder(4)]
    public string? Remarks { get; set; }

    [MemoryPackOrder(5)]
    public decimal TransactionFee { get; set; }
    
    [MemoryPackOrder(6)]
    public decimal ConvenienceFee { get; set; }
    
    [MemoryPackOrder(7)]
    public string? ReferenceNumber { get; set; }
    
    [MemoryPackOrder(8)]
    public string? Description { get; set; }

    [MemoryPackOrder(9)]
    public decimal? RunningTotalBalance { get; set; }
    
    [MemoryPackOrder(10)]
    public decimal? RunningAvailableBalance { get; set; }
   
    [MemoryPackOrder(11)]
    public decimal? RunningBalance { get; set; }
    
    [MemoryPackOrder(12)]
    public decimal? RunningDebitOnHoldBalance { get; set; }
    
    [MemoryPackOrder(13)]
    public decimal? RunningCreditOnHoldBalance { get; set; }
    
    [MemoryPackOrder(14)]
    public decimal PreviousTotalBalance { get; set; }
    
    [MemoryPackOrder(15)]
    public decimal PreviousBalance { get; set; }
    
    [MemoryPackOrder(16)]
    public decimal PreviousDebitOnHoldBalance { get; set; }
    
    [MemoryPackOrder(17)]
    public decimal PreviousCreditOnHoldBalance { get; set; }

    [MemoryPackOrder(18)]
    public TransactionType? TransactionType { get; set; }

    [MemoryPackOrder(19)]
    public virtual IdentityCredential Credential { get; set; } = null!;

    [MemoryPackOrder(20)]
    public virtual Wallet? Wallet { get; set; }
}

using System.ComponentModel.DataAnnotations.Schema;
using XFramework.Domain.Generic.Enums;

namespace XFramework.Domain.Generic.Contracts;

public partial class WalletTransaction : BaseModel
{
    public Guid CredentialId { get; set; }

    public Guid WalletId { get; set; }

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

    public bool Held { get; set; }
    
    public bool Released { get; set; }
    
    public string? Remarks { get; set; }
    
    public string? ReferenceNumber { get; set; }
    
    public string? Description { get; set; }

    public decimal? RunningTotalBalance { get; set; }
    public decimal? RunningAvailableBalance { get; set; }
   
    public decimal? RunningBalance { get; set; }
    
    public decimal? RunningDebitOnHoldBalance { get; set; }
    
    public decimal? RunningCreditOnHoldBalance { get; set; }
    
    public decimal PreviousTotalBalance { get; set; }
    
    public decimal PreviousBalance { get; set; }
    
    public decimal PreviousDebitOnHoldBalance { get; set; }
    
    public decimal PreviousCreditOnHoldBalance { get; set; }

    public TransactionType? TransactionType { get; set; }

    public virtual IdentityCredential Credential { get; set; } = null!;

    public virtual Wallet? Wallet { get; set; }
}
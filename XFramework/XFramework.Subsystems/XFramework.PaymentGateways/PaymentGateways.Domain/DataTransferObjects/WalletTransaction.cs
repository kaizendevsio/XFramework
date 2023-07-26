using System;
using System.Collections.Generic;

#nullable disable

namespace PaymentGateways.Domain.DataTransferObjects;

public partial class WalletTransaction
{
    public long Id { get; set; }
    public bool? IsEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public DateTime ModifiedAt { get; set; }
    public long? ModifiedBy { get; set; }
    public long IdentityCredentialId { get; set; }
    public long? SourceUserWalletId { get; set; }
    public decimal? Amount { get; set; }
    public string Remarks { get; set; }
    public decimal? RunningBalance { get; set; }
    public string Description { get; set; }
    public long? TargetUserWalletId { get; set; }
    public decimal? PreviousBalance { get; set; }
    public string Guid { get; set; }

    public virtual IdentityCredential IdentityCredential { get; set; }
    public virtual Wallet SourceUserWallet { get; set; }
    public virtual Wallet TargetUserWallet { get; set; }
}
using System;
using System.Collections.Generic;

#nullable disable

namespace PaymentGateways.Domain.DataTransferObjects;

public partial class Wallet
{
    public Wallet()
    {
        WalletAddresses = new HashSet<WalletAddress>();
        WalletTransactionSourceUserWallets = new HashSet<WalletTransaction>();
        WalletTransactionTargetUserWallets = new HashSet<WalletTransaction>();
    }

    public long Id { get; set; }
    public bool? IsEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public DateTime ModifiedAt { get; set; }
    public long? ModifiedBy { get; set; }
    public long IdentityCredentialId { get; set; }
    public long? WalletEntityId { get; set; }
    public decimal? Balance { get; set; }
    public bool? IsDeleted { get; set; }
    public string Guid { get; set; }

    public virtual IdentityCredential IdentityCredential { get; set; }
    public virtual WalletEntity WalletEntity { get; set; }
    public virtual ICollection<WalletAddress> WalletAddresses { get; set; }
    public virtual ICollection<WalletTransaction> WalletTransactionSourceUserWallets { get; set; }
    public virtual ICollection<WalletTransaction> WalletTransactionTargetUserWallets { get; set; }
}
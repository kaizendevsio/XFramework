using System;
using System.Collections.Generic;

namespace Wallets.Domain.DataTransferObjects
{
    public partial class TblUserWallet
    {
        public TblUserWallet()
        {
            TblUserWalletTransactionSourceUserWallets = new HashSet<TblUserWalletTransaction>();
            TblUserWalletTransactionTargetUserWallets = new HashSet<TblUserWalletTransaction>();
        }

        public long Id { get; set; }
        public bool? IsEnabled { get; set; }
        public DateTime? CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime? LastChanged { get; set; }
        public long UserAuthId { get; set; }
        public long? WalletTypeId { get; set; }
        public decimal? Balance { get; set; }
        public bool? IsDeleted { get; set; }
        public string Guid { get; set; }

        public virtual TblIdentityCredential UserAuth { get; set; }
        public virtual TblWalletEntity WalletType { get; set; }
        public virtual ICollection<TblUserWalletTransaction> TblUserWalletTransactionSourceUserWallets { get; set; }
        public virtual ICollection<TblUserWalletTransaction> TblUserWalletTransactionTargetUserWallets { get; set; }
    }
}

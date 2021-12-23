using System;
using System.Collections.Generic;

namespace IdentityServer.Domain.DataTransferObjects.Legacy
{
    public partial class TblUserWallet
    {
        public TblUserWallet()
        {
            TblUserWalletTransaction = new HashSet<TblUserWalletTransaction>();
        }

        public long Id { get; set; }
        public bool? IsEnabled { get; set; }
        public DateTime? CreatedOn { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime? LastChanged { get; set; }
        public long UserAuthId { get; set; }
        public long? WalletTypeId { get; set; }
        public decimal? Balance { get; set; }

        public virtual TblUserAuth UserAuth { get; set; }
        public virtual TblWalletType WalletType { get; set; }
        public virtual ICollection<TblUserWalletTransaction> TblUserWalletTransaction { get; set; }
    }
}

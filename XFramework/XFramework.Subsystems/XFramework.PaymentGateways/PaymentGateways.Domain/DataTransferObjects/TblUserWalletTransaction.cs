using System;
using System.Collections.Generic;

#nullable disable

namespace PaymentGateways.Domain.DataTransferObjects
{
    public partial class TblUserWalletTransaction
    {
        public long Id { get; set; }
        public bool? IsEnabled { get; set; }
        public DateTime? CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime? LastChanged { get; set; }
        public long UserAuthId { get; set; }
        public long? SourceUserWalletId { get; set; }
        public decimal? Amount { get; set; }
        public string Remarks { get; set; }
        public decimal? RunningBalance { get; set; }
        public string Description { get; set; }
        public long? TargetUserWalletId { get; set; }

        public virtual TblUserWallet SourceUserWallet { get; set; }
        public virtual TblUserWallet TargetUserWallet { get; set; }
        public virtual TblIdentityCredential UserAuth { get; set; }
    }
}

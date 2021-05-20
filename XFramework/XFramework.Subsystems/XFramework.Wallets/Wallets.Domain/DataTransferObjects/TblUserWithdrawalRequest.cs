using System;
using System.Collections.Generic;

#nullable disable

namespace Wallets.Domain.DataTransferObjects
{
    public partial class TblUserWithdrawalRequest
    {
        public long Id { get; set; }
        public bool? IsEnabled { get; set; }
        public DateTime? CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime? LastChanged { get; set; }
        public long UserAuthId { get; set; }
        public string Address { get; set; }
        public decimal? TotalAmount { get; set; }
        public short? WithdrawalStatus { get; set; }
        public string Remarks { get; set; }
        public long? SourceWalletTypeId { get; set; }
        public long? TargetCurrencyId { get; set; }

        public virtual TblWalletEntity SourceWalletType { get; set; }
        public virtual TblWalletEntity TargetCurrency { get; set; }
        public virtual TblIdentityCredential UserAuth { get; set; }
    }
}

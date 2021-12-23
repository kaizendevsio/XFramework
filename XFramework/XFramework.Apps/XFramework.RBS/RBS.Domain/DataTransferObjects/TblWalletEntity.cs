using System;
using System.Collections.Generic;

#nullable disable

namespace RBS.Domain.DataTransferObjects
{
    public partial class TblWalletEntity
    {
        public TblWalletEntity()
        {
            TblUserDepositRequests = new HashSet<TblUserDepositRequest>();
            TblUserWalletAddresses = new HashSet<TblUserWalletAddress>();
            TblUserWallets = new HashSet<TblUserWallet>();
            TblUserWithdrawalRequestSourceWalletTypes = new HashSet<TblUserWithdrawalRequest>();
            TblUserWithdrawalRequestTargetCurrencies = new HashSet<TblUserWithdrawalRequest>();
        }

        public long Id { get; set; }
        public bool? IsEnabled { get; set; }
        public DateTime? CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime? LastChanged { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string Desc { get; set; }
        public short Type { get; set; }
        public long? CurrencyEntityId { get; set; }
        public decimal? MinTransfer { get; set; }
        public decimal? MaxTransfer { get; set; }

        public virtual TblCurrencyEntity CurrencyEntity { get; set; }
        public virtual ICollection<TblUserDepositRequest> TblUserDepositRequests { get; set; }
        public virtual ICollection<TblUserWalletAddress> TblUserWalletAddresses { get; set; }
        public virtual ICollection<TblUserWallet> TblUserWallets { get; set; }
        public virtual ICollection<TblUserWithdrawalRequest> TblUserWithdrawalRequestSourceWalletTypes { get; set; }
        public virtual ICollection<TblUserWithdrawalRequest> TblUserWithdrawalRequestTargetCurrencies { get; set; }
    }
}

using System;
using System.Collections.Generic;

namespace IdentityServer.Domain.DataTransferObjects.Legacy
{
    public partial class TblCurrency
    {
        public TblCurrency()
        {
            TblAddressCountry = new HashSet<TblAddressCountry>();
            TblBusinessPackage = new HashSet<TblBusinessPackage>();
            TblExchangeRateSourceCurrency = new HashSet<TblExchangeRate>();
            TblExchangeRateTargetCurrency = new HashSet<TblExchangeRate>();
            TblUserDepositRequest = new HashSet<TblUserDepositRequest>();
            TblUserWithdrawalRequest = new HashSet<TblUserWithdrawalRequest>();
            TblWalletType = new HashSet<TblWalletType>();
        }

        public long Id { get; set; }
        public bool? IsEnabled { get; set; }
        public DateTime? CreatedOn { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime? LastChanged { get; set; }
        public string Name { get; set; }
        public string CurrencyIsoCode3 { get; set; }
        public string Description { get; set; }
        public short? CurrencyType { get; set; }

        public virtual ICollection<TblAddressCountry> TblAddressCountry { get; set; }
        public virtual ICollection<TblBusinessPackage> TblBusinessPackage { get; set; }
        public virtual ICollection<TblExchangeRate> TblExchangeRateSourceCurrency { get; set; }
        public virtual ICollection<TblExchangeRate> TblExchangeRateTargetCurrency { get; set; }
        public virtual ICollection<TblUserDepositRequest> TblUserDepositRequest { get; set; }
        public virtual ICollection<TblUserWithdrawalRequest> TblUserWithdrawalRequest { get; set; }
        public virtual ICollection<TblWalletType> TblWalletType { get; set; }
    }
}

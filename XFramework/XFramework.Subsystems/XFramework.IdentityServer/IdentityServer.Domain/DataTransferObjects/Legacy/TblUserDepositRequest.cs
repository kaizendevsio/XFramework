using System;
using System.Collections.Generic;

namespace IdentityServer.Domain.DataTransferObjects.Legacy
{
    public partial class TblUserDepositRequest
    {
        public TblUserDepositRequest()
        {
            TblUserBusinessPackage = new HashSet<TblUserBusinessPackage>();
        }

        public long Id { get; set; }
        public bool? IsEnabled { get; set; }
        public DateTime? CreatedOn { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime? LastChanged { get; set; }
        public long? UserAuthId { get; set; }
        public long? SourceCurrencyId { get; set; }
        public long? TargetWalletTypeId { get; set; }
        public string Address { get; set; }
        public decimal? Amount { get; set; }
        public string Remarks { get; set; }
        public short? DepositStatus { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public string RawData { get; set; }
        public string ReferenceNo { get; set; }
        public string RawResponseData { get; set; }

        public virtual TblCurrency SourceCurrency { get; set; }
        public virtual TblWalletType TargetWalletType { get; set; }
        public virtual TblUserAuth UserAuth { get; set; }
        public virtual ICollection<TblUserBusinessPackage> TblUserBusinessPackage { get; set; }
    }
}

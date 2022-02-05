using System;
using System.Collections.Generic;

namespace Wallets.Domain.DataTransferObjects
{
    public partial class TblUserBusinessPackage
    {
        public TblUserBusinessPackage()
        {
            TblUserBusinessPackageUpgradeTransactions = new HashSet<TblUserBusinessPackageUpgradeTransaction>();
            TblUserCommissionDeductionRequests = new HashSet<TblUserCommissionDeductionRequest>();
        }

        public long Id { get; set; }
        public bool? IsEnabled { get; set; }
        public DateTime? CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime? LastChanged { get; set; }
        public long? UserAuthId { get; set; }
        public long? BusinessPackageId { get; set; }
        public short? PackageStatus { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public long? UserDepositRequestId { get; set; }
        public DateTime? CancellationDate { get; set; }
        public DateTime? ActivationDate { get; set; }
        public long? RecipientAuthId { get; set; }
        public string CodeString { get; set; }
        public long? ConsumedBy { get; set; }
        public int? PackageType { get; set; }
        public string CodeHash { get; set; }
        public string Remarks { get; set; }

        public virtual TblIdentityCredential ConsumedByNavigation { get; set; }
        public virtual TblIdentityCredential RecipientAuth { get; set; }
        public virtual TblIdentityCredential UserAuth { get; set; }
        public virtual TblUserDepositRequest UserDepositRequest { get; set; }
        public virtual TblUserMap TblUserMap { get; set; }
        public virtual ICollection<TblUserBusinessPackageUpgradeTransaction> TblUserBusinessPackageUpgradeTransactions { get; set; }
        public virtual ICollection<TblUserCommissionDeductionRequest> TblUserCommissionDeductionRequests { get; set; }
    }
}

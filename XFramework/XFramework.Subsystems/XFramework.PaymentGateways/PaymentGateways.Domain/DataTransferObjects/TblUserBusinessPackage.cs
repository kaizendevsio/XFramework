using System;
using System.Collections.Generic;

#nullable disable

namespace PaymentGateways.Domain.DataTransferObjects
{
    public partial class TblUserBusinessPackage
    {
        public TblUserBusinessPackage()
        {
            TblUserMapUplineUsers = new HashSet<TblUserMap>();
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

        public virtual TblIdentityCredential ConsumedByNavigation { get; set; }
        public virtual TblIdentityCredential RecipientAuth { get; set; }
        public virtual TblIdentityCredential UserAuth { get; set; }
        public virtual TblUserDepositRequest UserDepositRequest { get; set; }
        public virtual TblUserMap TblUserMapIdNavigation { get; set; }
        public virtual ICollection<TblUserMap> TblUserMapUplineUsers { get; set; }
    }
}

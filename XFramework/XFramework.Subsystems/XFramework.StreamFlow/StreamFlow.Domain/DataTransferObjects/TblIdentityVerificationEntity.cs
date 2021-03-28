using System;
using System.Collections.Generic;

#nullable disable

namespace StreamFlow.Domain.DataTransferObjects
{
    public partial class TblIdentityVerificationEntity
    {
        public TblIdentityVerificationEntity()
        {
            TblIdentityVerifications = new HashSet<TblIdentityVerification>();
        }

        public long Id { get; set; }
        public bool? IsEnabled { get; set; }
        public DateTime? CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public string Name { get; set; }
        public long? DefaultExpiry { get; set; }
        public short? Priority { get; set; }
        public bool? IsDeleted { get; set; }

        public virtual ICollection<TblIdentityVerification> TblIdentityVerifications { get; set; }
    }
}

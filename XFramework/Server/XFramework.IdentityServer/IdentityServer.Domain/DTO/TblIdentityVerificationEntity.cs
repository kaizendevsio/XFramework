using System;
using System.Collections.Generic;

#nullable disable

namespace IdentityServer.Domain.DTO
{
    public partial class TblIdentityVerificationEntity
    {
        public TblIdentityVerificationEntity()
        {
            TblIdentityVerifications = new HashSet<TblIdentityVerification>();
        }

        public long Id { get; set; }
        public bool? IsEnabled { get; set; }
        public DateTime? CreatedOn { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public string Name { get; set; }
        public long? DefaultExpiry { get; set; }
        public short? Priority { get; set; }
        public bool? IsDeleted { get; set; }

        public virtual ICollection<TblIdentityVerification> TblIdentityVerifications { get; set; }
    }
}

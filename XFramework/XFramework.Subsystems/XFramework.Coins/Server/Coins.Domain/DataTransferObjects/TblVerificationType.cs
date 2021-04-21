using System;
using System.Collections.Generic;

#nullable disable

namespace Coins.Domain.DataTransferObjects
{
    public partial class TblVerificationType
    {
        public TblVerificationType()
        {
            TblIdentityVerifications = new HashSet<TblIdentityVerification>();
        }

        public long Id { get; set; }
        public bool? IsEnabled { get; set; }
        public DateTime? CreatedOn { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime? LastChanged { get; set; }
        public string Name { get; set; }
        public long? DefaultExpiry { get; set; }
        public short? Priority { get; set; }

        public virtual ICollection<TblIdentityVerification> TblIdentityVerifications { get; set; }
    }
}

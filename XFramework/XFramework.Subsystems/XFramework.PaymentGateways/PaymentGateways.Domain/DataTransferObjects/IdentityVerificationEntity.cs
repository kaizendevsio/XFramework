using System;
using System.Collections.Generic;

#nullable disable

namespace PaymentGateways.Domain.DataTransferObjects;

public partial class IdentityVerificationEntity
{
    public IdentityVerificationEntity()
    {
        IdentityVerifications = new HashSet<IdentityVerification>();
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
    public string Guid { get; set; }

    public virtual ICollection<IdentityVerification> IdentityVerifications { get; set; }
}
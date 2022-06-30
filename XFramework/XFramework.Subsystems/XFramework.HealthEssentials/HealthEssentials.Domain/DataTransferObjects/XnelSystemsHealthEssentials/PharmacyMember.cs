using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials
{
    public partial class PharmacyMember
    {
        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool IsDeleted { get; set; }
        public long CredentialId { get; set; }
        public long PharmacyId { get; set; }
        public string? Value { get; set; }
        public string Guid { get; set; } = null!;
        public string Name { get; set; } = null!;
        public int Status { get; set; }
        public string? Description { get; set; }
        public long? PharmacyLocationId { get; set; }

        public virtual Pharmacy Pharmacy { get; set; } = null!;
        public virtual PharmacyLocation? PharmacyLocation { get; set; }
    }
}

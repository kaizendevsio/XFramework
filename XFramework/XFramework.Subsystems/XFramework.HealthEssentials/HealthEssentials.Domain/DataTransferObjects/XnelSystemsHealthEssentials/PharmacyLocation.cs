using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials
{
    public partial class PharmacyLocation
    {
        public PharmacyLocation()
        {
            PharmacyJobOrders = new HashSet<PharmacyJobOrder>();
            PharmacyMembers = new HashSet<PharmacyMember>();
            PharmacyServices = new HashSet<PharmacyService>();
        }

        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool IsDeleted { get; set; }
        public long PharmacyId { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? UnitNumber { get; set; }
        public string? Street { get; set; }
        public string? Building { get; set; }
        public long? Barangay { get; set; }
        public long? City { get; set; }
        public string? Subdivision { get; set; }
        public long? Region { get; set; }
        public bool? MainAddress { get; set; }
        public long? Province { get; set; }
        public long? Country { get; set; }
        public string Guid { get; set; } = null!;
        public int? Status { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Website { get; set; }
        public string? AlternativePhone { get; set; }

        public virtual Pharmacy Pharmacy { get; set; } = null!;
        public virtual ICollection<PharmacyJobOrder> PharmacyJobOrders { get; set; }
        public virtual ICollection<PharmacyMember> PharmacyMembers { get; set; }
        public virtual ICollection<PharmacyService> PharmacyServices { get; set; }
    }
}

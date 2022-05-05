using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials
{
    public partial class Pharmacy
    {
        public Pharmacy()
        {
            PharmacyLocations = new HashSet<PharmacyLocation>();
            PharmacyMembers = new HashSet<PharmacyMember>();
            PharmacyServices = new HashSet<PharmacyService>();
            PharmacyStocks = new HashSet<PharmacyStock>();
            PharmacyTags = new HashSet<PharmacyTag>();
        }

        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool IsDeleted { get; set; }
        public long EntityId { get; set; }
        public string? Name { get; set; }
        public string? ShortName { get; set; }
        public string? Slogan { get; set; }
        public string? Description { get; set; }
        public string? ChemicalComponent { get; set; }
        public string Guid { get; set; } = null!;
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Website { get; set; }
        public string? Logo { get; set; }

        public virtual PharmacyEntity Entity { get; set; } = null!;
        public virtual ICollection<PharmacyLocation> PharmacyLocations { get; set; }
        public virtual ICollection<PharmacyMember> PharmacyMembers { get; set; }
        public virtual ICollection<PharmacyService> PharmacyServices { get; set; }
        public virtual ICollection<PharmacyStock> PharmacyStocks { get; set; }
        public virtual ICollection<PharmacyTag> PharmacyTags { get; set; }
    }
}

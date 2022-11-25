using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

public partial class Pharmacy
{
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

    public int Status { get; set; }

    public virtual PharmacyEntity Entity { get; set; } = null!;

    public virtual ICollection<PharmacyLocation> PharmacyLocations { get; } = new List<PharmacyLocation>();

    public virtual ICollection<PharmacyMember> PharmacyMembers { get; } = new List<PharmacyMember>();

    public virtual ICollection<PharmacyService> PharmacyServices { get; } = new List<PharmacyService>();

    public virtual ICollection<PharmacyStock> PharmacyStocks { get; } = new List<PharmacyStock>();

    public virtual ICollection<PharmacyTag> PharmacyTags { get; } = new List<PharmacyTag>();
}

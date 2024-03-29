﻿namespace HealthEssentials.Domain.Generics.Contracts;

public partial class HospitalServiceType : BaseModel
{
    public string Name { get; set; } = null!;

    public string? Description { get; set; }


    public Guid GroupId { get; set; }

    public int? SortOrder { get; set; }

    public virtual HospitalServiceTypeGroup Group { get; set; } = null!;

    public virtual ICollection<HospitalService> HospitalServices { get; set; } = new List<HospitalService>();
}
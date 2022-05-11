﻿namespace HealthEssentials.Domain.Generics.Contracts.Responses.Pharmacy;

public class PharmacyEntityResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public string Name { get; set; } = null!;
    public string Guid { get; set; } = null!;
    public int? SortOrder { get; set; }

}
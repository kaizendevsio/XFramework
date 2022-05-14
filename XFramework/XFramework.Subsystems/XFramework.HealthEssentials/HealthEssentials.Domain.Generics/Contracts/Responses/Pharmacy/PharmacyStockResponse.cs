﻿namespace HealthEssentials.Domain.Generics.Contracts.Responses.Pharmacy;

public class PharmacyStockResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public long PharmacyId { get; set; }
    public long MedicineId { get; set; }
    public DateTime LastRestock { get; set; }
    public long? AvailableQuantity { get; set; }
    public long? CriticalQuantity { get; set; }
    public long? MinQuantity { get; set; }
    public long? MaxQuantity { get; set; }
    public long? Unit { get; set; }
    public string Guid { get; set; } = null!;
}
﻿namespace HealthEssentials.Domain.Generics.Contracts.Responses.Laboratory;

public class LaboratoryServiceResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public long EntityId { get; set; }
    public long LaboratoryLocationId { get; set; }
    public long LaboratoryId { get; set; }
    public string? Name { get; set; }
    public string? ShortName { get; set; }
    public string? Description { get; set; }
    public decimal? Price { get; set; }
    public decimal? MaxDiscount { get; set; }
    public decimal? Quantity { get; set; }
    public long? UnitId { get; set; }
    public string Guid { get; set; } = null!;
}
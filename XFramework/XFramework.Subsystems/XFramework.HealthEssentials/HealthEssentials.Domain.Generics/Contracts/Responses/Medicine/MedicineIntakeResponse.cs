﻿using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

namespace HealthEssentials.Domain.Generics.Contracts.Responses.Medicine;

public class MedicineIntakeResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public long EntityId { get; set; }
    public string? Name { get; set; }
    public string? ScientificName { get; set; }
    public string? Description { get; set; }
    public long? Repetition { get; set; }
    public long? UnitId { get; set; }
    public string Guid { get; set; } = null!;

    public MedicineIntakeEntity Entity { get; set; } = null!;
}
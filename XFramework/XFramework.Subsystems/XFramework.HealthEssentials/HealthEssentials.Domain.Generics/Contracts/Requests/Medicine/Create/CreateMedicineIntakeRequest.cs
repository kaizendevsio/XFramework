﻿namespace HealthEssentials.Domain.Generics.Contracts.Requests.Medicine.Create;

public class CreateMedicineIntakeRequest : RequestBase
{
    public Guid? EntityGuid { get; set; }
    public string? Name { get; set; }
    public string? ScientificName { get; set; }
    public string? Description { get; set; }
    public long? Repetition { get; set; }
    public Guid? UnitGuid { get; set; }
}
﻿namespace HealthEssentials.Domain.Generics.Contracts.Responses.Ailment;

public class AilmentEntityGroupResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public string Name { get; set; } = null!;
    public string Guid { get; set; } = null!;
}
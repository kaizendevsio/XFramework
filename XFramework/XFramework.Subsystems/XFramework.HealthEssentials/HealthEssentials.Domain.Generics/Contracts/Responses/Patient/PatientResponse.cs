﻿namespace HealthEssentials.Domain.Generics.Contracts.Responses.Patient;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;


public class PatientResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public long EntityId { get; set; }
    public long CredentialId { get; set; }
    public string? Description { get; set; }
    public string? Remarks { get; set; }
    public string Guid { get; set; } = null!;

    public PatientEntity Entity { get; set; } = null!;
}
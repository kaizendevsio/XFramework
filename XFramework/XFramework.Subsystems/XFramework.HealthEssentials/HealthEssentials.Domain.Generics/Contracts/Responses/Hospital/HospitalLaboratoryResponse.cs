namespace HealthEssentials.Domain.Generics.Contracts.Responses.Hospital;

public class HospitalLaboratoryResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public long HospitalId { get; set; }
    public long? LaboratoryId { get; set; }
    public string Guid { get; set; } = null!;   
    
    /*public Hospital Hospital { get; set; } = null!;*/
}
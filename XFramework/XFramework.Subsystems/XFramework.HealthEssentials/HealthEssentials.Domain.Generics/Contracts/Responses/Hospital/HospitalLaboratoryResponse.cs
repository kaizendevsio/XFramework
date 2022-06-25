using HealthEssentials.Domain.Generics.Contracts.Responses.Laboratory;

namespace HealthEssentials.Domain.Generics.Contracts.Responses.Hospital;

public class HospitalLaboratoryResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public string Guid { get; set; } = null!;   
    
    public HospitalResponse Hospital { get; set; } = null!;
    public LaboratoryResponse? Laboratory { get; set; }
}
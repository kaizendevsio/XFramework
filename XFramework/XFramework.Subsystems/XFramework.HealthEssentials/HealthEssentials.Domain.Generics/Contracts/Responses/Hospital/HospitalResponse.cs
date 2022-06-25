

namespace HealthEssentials.Domain.Generics.Contracts.Responses.Hospital;

public class HospitalResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public long EntityId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string? Remarks { get; set; }
    public Guid? Guid { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? Logo { get; set; }
    
    public HospitalEntityResponse? Entity { get; set; }
}
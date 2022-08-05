namespace HealthEssentials.Domain.Generics.Contracts.Responses.Hospital;

public class HospitalLocationResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? UnitNumber { get; set; }
    public string? Street { get; set; }
    public string? Building { get; set; }
    public long? Barangay { get; set; }
    public long? City { get; set; }
    public string? Subdivision { get; set; }
    public long? Region { get; set; }
    public bool? MainAddress { get; set; }
    public long? Province { get; set; }
    public long? Country { get; set; }
    public Guid? Guid { get; set; }

    public HospitalResponse? Hospital { get; set; }
}
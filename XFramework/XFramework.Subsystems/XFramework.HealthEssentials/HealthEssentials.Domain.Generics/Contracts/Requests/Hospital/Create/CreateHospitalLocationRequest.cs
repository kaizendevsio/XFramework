namespace HealthEssentials.Domain.Generics.Contracts.Requests.Hospital.Create;

public class CreateHospitalLocationRequest : RequestBase
{
    public Guid HospitalGuid { get; set; }
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
}
namespace HealthEssentials.Domain.Generics.Contracts.Requests.Hospital.Update;

public class UpdateHospitalServiceRequest : RequestBase
{
    public Guid? EntityGuid { get; set; }
    public Guid? HospitalLocationGuid { get; set; }
    public Guid? HospitalGuid { get; set; }
    public string? Name { get; set; }
    public string? ShortName { get; set; }
    public string? Description { get; set; }
    public decimal? Price { get; set; }
    public decimal? MaxDiscount { get; set; }
    public decimal? Quantity { get; set; }
    public Guid? UnitGuid { get; set; }
}
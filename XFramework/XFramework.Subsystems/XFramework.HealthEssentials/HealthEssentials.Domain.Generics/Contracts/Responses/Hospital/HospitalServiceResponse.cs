

using HealthEssentials.Domain.Generics.Contracts.Responses.Unit;

namespace HealthEssentials.Domain.Generics.Contracts.Responses.Hospital;

public class HospitalServiceResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public string? Name { get; set; }
    public string? ShortName { get; set; }
    public string? Description { get; set; }
    public decimal? Price { get; set; }
    public decimal? MaxDiscount { get; set; }
    public decimal? Quantity { get; set; }
    public Guid? Guid { get; set; }
    
    public HospitalServiceEntityResponse? Entity { get; set; }
    public HospitalLocationResponse? HospitalLocation { get; set; }
    public HospitalResponse? Hospital { get; set; }
    public UnitResponse? Unit { get; set; }
}
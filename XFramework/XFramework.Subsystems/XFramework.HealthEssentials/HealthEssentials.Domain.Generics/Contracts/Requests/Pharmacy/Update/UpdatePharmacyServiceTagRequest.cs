namespace HealthEssentials.Domain.Generics.Contracts.Requests.Pharmacy.Update;

public class UpdatePharmacyServiceTagRequest : RequestBase
{
    public Guid? PharmacyServiceGuid { get; set; }
    public string? Value { get; set; }
    public Guid? TagGuid { get; set; }
}
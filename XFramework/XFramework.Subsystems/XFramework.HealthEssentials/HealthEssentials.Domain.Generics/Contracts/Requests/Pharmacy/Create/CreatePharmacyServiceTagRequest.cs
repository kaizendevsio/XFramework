namespace HealthEssentials.Domain.Generics.Contracts.Requests.Pharmacy.Create;

public class CreatePharmacyServiceTagRequest : RequestBase
{
    public Guid? PharmacyServiceGuid { get; set; }
    public string? Value { get; set; }
    public Guid? TagGuid { get; set; }
}
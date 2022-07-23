namespace HealthEssentials.Domain.Generics.Contracts.Requests.Pharmacy.Update;

public class UpdatePharmacyTagRequest : RequestBase
{
    public Guid? PharmacyGuid { get; set; }
    public string? Value { get; set; }
    public Guid? TagGuid { get; set; }
}
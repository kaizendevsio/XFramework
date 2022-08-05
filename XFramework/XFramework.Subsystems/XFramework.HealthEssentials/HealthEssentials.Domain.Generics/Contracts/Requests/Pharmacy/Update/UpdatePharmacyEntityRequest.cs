namespace HealthEssentials.Domain.Generics.Contracts.Requests.Pharmacy.Update;

public class UpdatePharmacyEntityRequest : RequestBase
{
    public string? Name { get; set; }
    public int? SortOrder { get; set; }
}
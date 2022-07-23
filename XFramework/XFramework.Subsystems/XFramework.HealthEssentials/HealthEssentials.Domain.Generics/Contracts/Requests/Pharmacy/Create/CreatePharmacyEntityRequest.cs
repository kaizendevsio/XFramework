namespace HealthEssentials.Domain.Generics.Contracts.Requests.Pharmacy.Create;

public class CreatePharmacyEntityRequest : RequestBase
{
    public string? Name { get; set; }
    public int? SortOrder { get; set; }
}
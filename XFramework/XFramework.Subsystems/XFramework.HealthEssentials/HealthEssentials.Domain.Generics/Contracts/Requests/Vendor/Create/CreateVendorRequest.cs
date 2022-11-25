namespace HealthEssentials.Domain.Generics.Contracts.Requests.Vendor.Create;

public class CreateVendorRequest : RequestBase
{
    public Guid? EntityGuid { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public bool? IsGenericProvider { get; set; }
}
namespace HealthEssentials.Domain.Generics.Contracts.Requests.Vendor.Update;

public class UpdateVendorRequest : RequestBase
{
    public Guid? EntityGuid { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public bool? IsGenericProvider { get; set; }
}
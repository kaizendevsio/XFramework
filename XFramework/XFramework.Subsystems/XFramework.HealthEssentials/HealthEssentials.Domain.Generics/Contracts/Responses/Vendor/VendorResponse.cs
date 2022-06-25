

namespace HealthEssentials.Domain.Generics.Contracts.Responses.Vendor;

public class VendorResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public bool? IsGenericProvider { get; set; }
    public string Guid { get; set; } = null!;

    public VendorEntityResponse Entity { get; set; } = null!;
}
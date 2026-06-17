using XFramework.Domain.Shared.Contracts.Base;

namespace XFramework.Inventario.Domain.Shared.Contracts;

public class Warehouse : BaseModel
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? AddressLine { get; set; }
    public string? City { get; set; }
    public string? Region { get; set; }
    public string? PostalCode { get; set; }
    public string? CountryCode { get; set; }
    public bool IsDefault { get; set; }
    public List<InventoryLocation> Locations { get; set; } = new();
}

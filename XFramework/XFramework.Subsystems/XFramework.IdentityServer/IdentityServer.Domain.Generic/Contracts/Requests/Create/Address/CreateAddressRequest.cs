using XFramework.Domain.Generic.Contracts.Requests;

namespace IdentityServer.Domain.Generic.Contracts.Requests.Create.Address;

public class CreateAddressRequest : RequestBase
{
    public Guid? AddressEntityGuid { get; set; }
    public Guid? IdentityInfoGuid { get; set; }
    
    public string Name { get; set; }
    public string UnitNumber { get; set; }
    public string Street { get; set; }
    public string Building { get; set; }
    public Guid? BarangayGuid { get; set; }
    public Guid? CityGuid { get; set; }
    public string Subdivision { get; set; }
    public Guid? RegionGuid { get; set; }
    public bool? DefaultAddress { get; set; }
    public Guid? ProvinceGuid { get; set; }
    public Guid? CountryGuid { get; set; }
}
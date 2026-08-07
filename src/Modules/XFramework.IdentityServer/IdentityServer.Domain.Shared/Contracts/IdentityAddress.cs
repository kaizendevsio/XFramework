using XFramework.Domain.Shared.Attributes;

namespace IdentityServer.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
[AllowRemoteDataContextMutation]
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.All,
    RoutePrefix = "api/identity-addresses",
    RequireAuthorization = true,
    AuthorizationFeature = "identity.addresses"
)]
public partial class IdentityAddress : BaseModel
{
    [MemoryPackOrder(0)]
    public Guid IdentityInfoId { get; set; }

    [MemoryPackOrder(1)]
    public string? UnitNumber { get; set; }

    [MemoryPackOrder(2)]
    public string? Street { get; set; }

    [MemoryPackOrder(3)]
    public string? Building { get; set; }

    [MemoryPackOrder(4)]
    public string? Name { get; set; }

    [MemoryPackOrder(5)]
    public Guid? BarangayId { get; set; }

    [MemoryPackOrder(6)]
    public Guid? CityId { get; set; }

    [MemoryPackOrder(7)]
    public string? Subdivision { get; set; }

    [MemoryPackOrder(8)]
    public Guid? RegionId { get; set; }

    [MemoryPackOrder(9)]
    public Guid? AddressTypeId { get; set; }

    [MemoryPackOrder(10)]
    public bool? DefaultAddress { get; set; }

    [MemoryPackOrder(11)]
    public Guid? ProvinceId { get; set; }

    [MemoryPackOrder(12)]
    public Guid? CountryId { get; set; }

    [MemoryPackOrder(13)]
    public double? Latitude { get; set; }

    [MemoryPackOrder(14)]
    public double? Longitude { get; set; }

    [MemoryPackOrder(15)]
    public DateTime? LastUpdated { get; set; }

    [MemoryPackOrder(16)]
    public virtual IdentityAddressType? AddressType { get; set; }

    [MemoryPackOrder(17)]
    public virtual AddressBarangay? Barangay { get; set; }

    [MemoryPackOrder(18)]
    public virtual AddressCity? City { get; set; }

    [MemoryPackOrder(19)]
    public virtual AddressCountry? Country { get; set; }

    [MemoryPackOrder(20)]
    public virtual IdentityInformation? IdentityInfo { get; set; }

    [MemoryPackOrder(21)]
    public virtual AddressProvince? Province { get; set; }

    [MemoryPackOrder(22)]
    public virtual AddressRegion? Region { get; set; }

    [MemoryPackOrder(23)]
    public string? ConsolidatedName { get; set; }
}

public class CreateIdentityAddressRequest
{
    public Guid IdentityInfoId { get; set; }
    public string? UnitNumber { get; set; }
    public string? Street { get; set; }
    public string? Building { get; set; }
    public string? Name { get; set; }
    public Guid? BarangayId { get; set; }
    public Guid? CityId { get; set; }
    public string? Subdivision { get; set; }
    public Guid? RegionId { get; set; }
    public Guid? AddressTypeId { get; set; }
    public bool? DefaultAddress { get; set; }
    public Guid? ProvinceId { get; set; }
    public Guid? CountryId { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? ConsolidatedName { get; set; }
}

public class UpdateIdentityAddressRequest
{
    public Guid IdentityInfoId { get; set; }
    public string? UnitNumber { get; set; }
    public string? Street { get; set; }
    public string? Building { get; set; }
    public string? Name { get; set; }
    public Guid? BarangayId { get; set; }
    public Guid? CityId { get; set; }
    public string? Subdivision { get; set; }
    public Guid? RegionId { get; set; }
    public Guid? AddressTypeId { get; set; }
    public bool? DefaultAddress { get; set; }
    public Guid? ProvinceId { get; set; }
    public Guid? CountryId { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? ConsolidatedName { get; set; }
}

public class GetIdentityAddressListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
    public Guid? IdentityInfoId { get; set; }
    public Guid? AddressTypeId { get; set; }
    public Guid? CityId { get; set; }
    public Guid? ProvinceId { get; set; }
    public Guid? RegionId { get; set; }
    public Guid? CountryId { get; set; }
}

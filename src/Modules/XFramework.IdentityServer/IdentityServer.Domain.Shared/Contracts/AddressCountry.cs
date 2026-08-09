using XFramework.Domain.Shared.Attributes;

namespace IdentityServer.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.ReadOnly,
    RoutePrefix = "api/address-countries",
    RequireAuthorization = true,
    AuthorizationFeature = "identity.addresses"
)]
public partial class AddressCountry : BaseModel
{
    
    [MemoryPackOrder(0)]
    public string? IsoCode2 { get; set; }

    [MemoryPackOrder(1)]
    public string? IsoCode3 { get; set; }

    [MemoryPackOrder(2)]
    public string? Name { get; set; }

    [MemoryPackOrder(3)]
    public string? Language { get; set; }

    [MemoryPackOrder(4)]
    public string? PhoneCountryCode { get; set; }

    [MemoryPackOrder(5)]
    public Guid CurrencyId { get; set; }


    [MemoryPackOrder(6)]
    public virtual ICollection<AddressRegion> AddressRegions { get; set; } = new List<AddressRegion>();

    [MemoryPackOrder(8)]
    public virtual ICollection<IdentityAddress> IdentityAddresses { get; set; } = new List<IdentityAddress>();
}

public class GetAddressCountryListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
    public string? IsoCode2 { get; set; }
    public string? IsoCode3 { get; set; }
}

using XFramework.Domain.Shared.Attributes;

namespace IdentityServer.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.ReadOnly,
    RoutePrefix = "api/address-provinces",
    RequireAuthorization = true
)]
public partial class AddressProvince : BaseModel
{
    
    [MemoryPackOrder(0)]
    public long PsgcCode { get; set; }

    [MemoryPackOrder(1)]
    public string? Description { get; set; }

    [MemoryPackOrder(2)]
    public long RegCodeId { get; set; }

    [MemoryPackOrder(3)]
    public long Code { get; set; }


    [MemoryPackOrder(4)]
    public virtual ICollection<AddressCity> AddressCities { get; set; } = new List<AddressCity>();

    [MemoryPackOrder(5)]
    public virtual ICollection<IdentityAddress> IdentityAddresses { get; set; } = new List<IdentityAddress>();

    [MemoryPackOrder(6)]
    public virtual AddressRegion RegCode { get; set; } = null!;
}

public class GetAddressProvinceListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
    public long? RegCodeId { get; set; }
}

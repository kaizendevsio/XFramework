using XFramework.Domain.Shared.Attributes;

namespace IdentityServer.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.ReadOnly,
    RoutePrefix = "api/address-cities",
    RequireAuthorization = true
)]
public partial class AddressCity : BaseModel
{
    
    [MemoryPackOrder(0)]
    public long PsgcCode { get; set; }

    [MemoryPackOrder(1)]
    public string? Description { get; set; }

    [MemoryPackOrder(2)]
    public long ProvCodeId { get; set; }

    [MemoryPackOrder(3)]
    public long Code { get; set; }

    [MemoryPackOrder(4)]
    public int? RegCode { get; set; }


    [MemoryPackOrder(5)]
    public virtual ICollection<AddressBarangay> AddressBarangays { get; set; } = new List<AddressBarangay>();

    [MemoryPackOrder(6)]
    public virtual ICollection<IdentityAddress> IdentityAddresses { get; set; } = new List<IdentityAddress>();

    [MemoryPackOrder(7)]
    public virtual AddressProvince ProvCode { get; set; } = null!;
}

public class GetAddressCityListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
    public long? ProvCodeId { get; set; }
}

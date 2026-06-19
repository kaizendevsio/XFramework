using XFramework.Domain.Shared.Attributes;

namespace Wallets.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.Get | EndpointActions.GetList,
    RoutePrefix = "api/wallet-addresses",
    RequireAuthorization = true,
    CacheDurationSeconds = 600,
    CacheKeyPrefix = "wallet-addresses"
)]
public partial class WalletAddress : BaseModel
{
    
    [MemoryPackOrder(0)]
    public string? Address { get; set; }

    [MemoryPackOrder(1)]
    public decimal? Balance { get; set; }

    [MemoryPackOrder(2)]
    public string? Remarks { get; set; }


    [MemoryPackOrder(3)]
    public Guid WalletId { get; set; }

    [MemoryPackOrder(4)]
    public virtual Wallet Wallet { get; set; } = null!;
}

public class CreateWalletAddressRequest
{
    public string? Address { get; set; }
    public decimal? Balance { get; set; }
    public string? Remarks { get; set; }
    public Guid WalletId { get; set; }
}

public class UpdateWalletAddressRequest
{
    public string? Address { get; set; }
    public decimal? Balance { get; set; }
    public string? Remarks { get; set; }
}

public class GetWalletAddressListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
    public Guid? WalletId { get; set; }
}


namespace XFramework.Client.Shared.Core.Features.Cache;

public partial class CacheState : State<CacheState>
{
    public override void Initialize()
    {
    }

    public List<AddressCountryResponse> AddressEntityList { get; set; }
}
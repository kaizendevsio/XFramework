using XFramework.Client.Shared.Entity.Models.Responses.CoinCap;

namespace XFramework.Client.Shared.Core.Features.Cryptocurrency;

public partial class CryptocurrencyState : State<CryptocurrencyState>
{
    public override void Initialize()
    {
        
    }

    public List<AssetResponse> AssetList { get; set; }
    public AssetResponse SelectedAsset { get; set; }
}
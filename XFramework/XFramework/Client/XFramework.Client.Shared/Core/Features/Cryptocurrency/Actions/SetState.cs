using XFramework.Client.Shared.Entity.Models.Responses.CoinCap;

namespace XFramework.Client.Shared.Core.Features.Cryptocurrency;

public partial class CryptocurrencyState
{
    public class SetState : IAction
    {
        public List<AssetResponse> AssetList { get; set; }
        public AssetResponse SelectedAsset { get; set; }
    }
} 

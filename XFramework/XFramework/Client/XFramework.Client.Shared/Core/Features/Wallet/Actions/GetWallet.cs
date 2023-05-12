using XFramework.Client.Shared.Entity.Models.Requests.Common;

namespace XFramework.Client.Shared.Core.Features.Wallet;

public partial class WalletState
{
    public class GetWallet : NavigableRequest, IAction
    {
        public Guid WalletGuid { get; set; }
    }
}
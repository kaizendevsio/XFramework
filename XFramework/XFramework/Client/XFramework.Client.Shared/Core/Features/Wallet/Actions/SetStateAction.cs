using Wallets.Domain.Generic.Contracts.Responses;

namespace XFramework.Client.Shared.Core.Features.Wallet;

public partial class WalletState
{
    public class SetState : IAction
    {
        public List<WalletResponse> WalletList { get; set; }
    }
}
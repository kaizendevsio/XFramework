namespace XFramework.Client.Shared.Core.Features.Wallet;

public partial class WalletState
{
    public class GetWalletListAction : IAction
    {
        public string NavigateToOnSuccess { get; set; }
        public string NavigateToOnFailure { get; set; }
    }
}
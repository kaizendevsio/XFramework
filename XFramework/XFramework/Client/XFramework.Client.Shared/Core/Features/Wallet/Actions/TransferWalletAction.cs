namespace XFramework.Client.Shared.Core.Features.Wallet;

public partial class WalletState
{
    public class TransferWalletAction : IAction
    {
        public string NavigateToOnSuccess { get; set; }
        public string NavigateToOnFailure { get; set; }
    }
}
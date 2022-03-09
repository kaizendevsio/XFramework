namespace XFramework.Client.Shared.Core.Features.Wallet;

public partial class WalletState
{
    public class CreateWallet : IAction
    {
        public bool ReloadWalletList { get; set; }
        public Guid WalletEntityGuid { get; set; }
        public long Balance { get; set; } = 0;
    }
}
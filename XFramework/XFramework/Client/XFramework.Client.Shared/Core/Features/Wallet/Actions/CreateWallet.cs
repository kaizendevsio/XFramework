namespace XFramework.Client.Shared.Core.Features.Wallet;

public partial class WalletState
{
    public class CreateWallet : IAction
    {
        public bool ReloadWalletList { get; set; }
        public Guid WalletEntityGuid { get; set; }
        public decimal Balance { get; set; } = 0;
        public Guid? CredentialGuid { get; set; }
        public bool Silent { get; set; }
    }
}
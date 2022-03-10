using Wallets.Domain.Generic.Contracts.Responses;

namespace XFramework.Client.Shared.Core.Features.Wallet;

public partial class WalletState : State<WalletState>
{
    public override void Initialize()
    {
        WalletList = new();
    }
    
    public List<WalletResponse> WalletList { get; set; }
    public Action InvokeRefresh { get; set; }
}
using Wallets.Domain.Generic.Contracts.Responses;
using XFramework.Client.Shared.Entity.Models.Requests.Wallet;

namespace XFramework.Client.Shared.Core.Features.Wallet;

public partial class WalletState : State<WalletState>
{
    public override void Initialize()
    {
    }
    
    public List<WalletResponse> WalletList { get; set; }
    public SendWalletRequest SendWalletVm { get; set; } = new();
    public SendWalletRequest CurrentTransactionVm { get; set; } = new();
    
    public Action InvokeRefresh { get; set; }
    public Timer Timer { get; set; }
}
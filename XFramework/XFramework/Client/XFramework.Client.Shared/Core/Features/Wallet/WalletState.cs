using XFramework.Client.Shared.Entity.Models.Requests.Wallet;

namespace XFramework.Client.Shared.Core.Features.Wallet;

public partial class WalletState : State<WalletState>
{
    public override void Initialize()
    {
    }
    
    public List<Domain.Generic.Contracts.Wallet>? WalletList { get; set; }
    public Domain.Generic.Contracts.Wallet? Selected { get; set; }
    public SendWalletRequest SendWalletVm { get; set; } = new();
    public SendWalletRequest CurrentTransactionVm { get; set; } = new();
    
    public Action? InvokeRefresh { get; set; }
    public Timer Timer { get; set; }
}
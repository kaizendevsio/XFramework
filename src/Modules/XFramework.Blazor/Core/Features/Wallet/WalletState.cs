namespace XFramework.Blazor.Core.Features.Wallet;

public partial class WalletState : State<WalletState>
{
    public override void Initialize()
    {
    }
    
    public List<Wallets.Domain.Shared.Contracts.Wallet>? WalletList { get; set; }
    public List<WalletTransaction>? TransactionList { get; set; }
    public Wallets.Domain.Shared.Contracts.Wallet? Selected { get; set; }
    public WalletTransaction? CurrentTransaction { get; set; }
    public TransferWallet? PendingPayment { get; set; }
    
    public Action? InvokeRefresh { get; set; }
    public Timer Timer { get; set; }
}
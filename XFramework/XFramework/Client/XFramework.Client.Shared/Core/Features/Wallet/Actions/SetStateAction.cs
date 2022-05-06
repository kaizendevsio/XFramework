using Wallets.Domain.Generic.Contracts.Responses;
using XFramework.Client.Shared.Entity.Models.Requests.Wallet;

namespace XFramework.Client.Shared.Core.Features.Wallet;

public partial class WalletState
{
    public class SetState : IAction
    {
        public List<WalletResponse> WalletList { get; set; }
        public SendWalletRequest CurrentTransactionVm { get; set; }
    }
}
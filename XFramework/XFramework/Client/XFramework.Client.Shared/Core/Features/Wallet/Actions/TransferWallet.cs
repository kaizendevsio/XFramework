using XFramework.Client.Shared.Entity.Models.Requests.Common;
using XFramework.Domain.Generic.Contracts.Requests;

namespace XFramework.Client.Shared.Core.Features.Wallet;

public partial class WalletState
{
    public class TransferWallet : NavigableRequest, IAction
    {
    }
}
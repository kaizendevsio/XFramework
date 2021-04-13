using MediatR;

namespace XFramework.Core.DataAccess.Commands.Entity.UserWallet
{
    public class CreateWalletTransactionCmd : WalletTransactionBO, IRequest<bool>
    {
    }
}

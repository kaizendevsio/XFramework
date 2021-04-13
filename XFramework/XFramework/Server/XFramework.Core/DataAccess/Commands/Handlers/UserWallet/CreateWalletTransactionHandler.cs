using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using XFramework.Core.DataAccess.Commands.Entity.UserWallet;

namespace XFramework.Core.DataAccess.Commands.Handlers.UserWallet
{
    public class CreateWalletTransactionHandler : CommandBaseHandler, IRequestHandler<CreateWalletTransactionCmd, bool>
    {
        public Task<bool> Handle(CreateWalletTransactionCmd request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}

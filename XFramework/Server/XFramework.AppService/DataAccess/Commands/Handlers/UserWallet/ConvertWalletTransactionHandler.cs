using MediatR;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XFramework.Core.DataAccess.Commands.Entity.UserWallet;

namespace XFramework.Core.DataAccess.Commands.Handlers.UserWallet
{
    public class ConvertWalletTransactionHandler : CommandBaseHandler, IRequestHandler<ConvertWalletTransactionCmd, bool>
    {
        public Task<bool> Handle(ConvertWalletTransactionCmd request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}

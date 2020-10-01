using MediatR;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XFramework.Core.DataAccess.Commands.Handlers;
using XFramework.Core.DataAccess.Query.Entity.UserWallet;

namespace XFramework.Core.DataAccess.Query.Handlers.UserWallet
{
    public class GetUserWalletTransactionHandler : QueryBaseHandler, IRequestHandler<GetUserWalletTransactionQuery, List<bool>>
    {
        public Task<List<bool>> Handle(GetUserWalletTransactionQuery request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}

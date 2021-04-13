using MediatR;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using XFramework.Core.DataAccess.Query.Entity.UserWallet;

namespace XFramework.Core.DataAccess.Query.Handlers.UserWallet
{
    public class GetUserWalletHandler : QueryBaseHandler, IRequestHandler<GetUserWalletQuery, List<bool>>
    {
        public Task<List<bool>> Handle(GetUserWalletQuery request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}

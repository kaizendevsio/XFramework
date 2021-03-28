using MediatR;
using System;
using System.Collections.Generic;
using System.Text;
using XFramework.Domain.BusinessObjects;

namespace XFramework.Core.DataAccess.Query.Entity.UserWallet
{
    public class GetUserWalletTransactionQuery : UserAuthBO, IRequest<List<bool>>
    {
    }
}
